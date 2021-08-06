using InstagramApiSharp.Classes;
using InstagramApiSharp.Classes.Models;
using InstagramManager.Data.Models;
using Microsoft.Extensions.Hosting;
using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace InstagramManager.Social.Telegram.Handlers
{
    public partial class UpdateHandler
    {
        private async Task HandleMessage(Message m, CancellationToken ct)
        {
            if (m.Chat.Type != ChatType.Private)
                return;

            var userId = m.From.Id;
            var status = await _context.Users.Find(u => u.Id == userId).Project(u => u.Status).SingleOrDefaultAsync(ct);

            Task SetStatusAsync(string newStatus)
            {
                return _context.Users.UpdateOneAsync(u => u.Id == userId, Builders<Person>.Update
                    .Set(u => u.Status, newStatus), cancellationToken: ct);
            }

            var isBlocked = await _context.Users.Find(s => s.Id == m.From.Id).Project(u => u.IsBlocked).FirstOrDefaultAsync();
            if (isBlocked)
            {
                await _bot.SendTextMessageAsync(userId,
                    $"Вы заблокированы, для разблокировки обратитесь к [администратору](tg://user?id={_instaOptions.Value.AdminId})",
                    ParseMode.Markdown, 
                    cancellationToken: ct);
                return;
            }

            // TODO handle authorization if Person.LoginStatus != null
            
            switch (m)
            {
                //Admin Panel
                case { Text: { } t } when m.From.Id == _instaOptions.Value.AdminId && m.Text.StartsWith("!"):
                    {
                        var s = m.Text.Split(' ');
                        
                        if (s[0] == "!добавить_промокод" && s.Length == 3)
                        {
                            int.TryParse(s[1], out var countOfCodes);
                            int.TryParse(s[2], out var countOfCrowns);
                            if (countOfCodes < 1 || countOfCodes > 20 || countOfCrowns < 1)
                                return;

                            GeneratorDeepURL.GeneratorDeepURL gd = new GeneratorDeepURL.GeneratorDeepURL(_context);
                            var result = await gd.GenerateUrlPromocode(countOfCodes, countOfCrowns, ct);
                            var text = string.Join("\n", result.Select((r, i) => $"{i + 1}. {r}"));
                            await _bot.SendTextMessageAsync(userId, text, cancellationToken: ct);
                        }
                      
                        else if (s[0] == "!удалить_промокод" && s.Length == 2)
                        {
                            try
                            {
                                var separated = s[1].Split('=');
                                if (separated.Length != 2)
                                    return;

                                await _context.Promocodes.DeleteOneAsync(sp=> sp.Id == separated[1]);
                                await _bot.SendTextMessageAsync(userId, "Промокод удалён.", ParseMode.Markdown, cancellationToken: ct);

                            }
                            catch (Exception e)
                            {
                                //Заглушка
                            }
                            
                        }
                        else if(s[0] == "!заблокировать" && s.Length == 2 && int.TryParse(s[1], out var idToBlock))
                        {
                            await _context.Users.UpdateOneAsync(u => u.Id == idToBlock, Builders<Person>.Update.Set(s => s.IsBlocked, true),
                                new UpdateOptions
                                {
                                    IsUpsert = false
                                },
                                cancellationToken: ct);
                        }
                        else
                        {
                            await _bot.SendTextMessageAsync(userId, "Команда не найдена", ParseMode.Markdown, cancellationToken: ct);

                        }
                        break;
                    }
                // start
                case { Text: { } t } when t.StartsWith("/start") /*&& status == null*/:
                    {
                        // /start
                        // /start <promocode>
                        // /start invite <user_id>
                        var separated = t.Split(' ');

                        // /start <promocode> обработали
                        if (separated.Length == 2)
                        {
                            var cashToIncrement = await _context.Promocodes.FindOneAndUpdateAsync(u => u.Id == separated[1] && !u.IsUsed, Builders<Promocodes>.Update
                                    .Set(p => p.IsUsed, true).Set(p => p.UsedBy, userId),
                                    new FindOneAndUpdateOptions<Promocodes, int>
                                    {
                                        IsUpsert = false,
                                        ReturnDocument = ReturnDocument.After,
                                        Projection = Builders<Promocodes>.Projection.Expression(p => p.Cash)
                                    },
                                    cancellationToken: ct);
                            if (cashToIncrement == 0)
                            {
                                await _bot.SendTextMessageAsync(userId, "Промокод уже был ранее использован.", ParseMode.Markdown, cancellationToken: ct);
                                return;
                            }

                            // Set это перезапись. Начисление через Inc (increment)
                            var t1 = _context.Users.UpdateOneAsync(u => u.Id == userId, Builders<Person>.Update.Inc(u => u.Crowns, cashToIncrement), cancellationToken: ct);
                            var t2 = _bot.SendTextMessageAsync(userId, string.Format(_config["success_activate_code"]), ParseMode.Markdown, cancellationToken: ct);

                            await Task.WhenAll(t1, t2);
                            return;
                        }

                        // /start invite <user_id>
                        if (separated.Length == 3)
                        {
                            switch (separated[1])
                            {
                                case "invite" when int.TryParse(separated[2], out var invitedBy) &&
                                        await _context.Users.Find(u => u.Id == invitedBy).AnyAsync(ct):
                                    {
                                        await _context.Users.UpdateOneAsync(u => u.Id == userId, Builders<Person>.Update
                                            .SetOnInsert(u => u.Status, "begin").SetOnInsert(u => u.ActiveTask, ObjectId.Empty).SetOnInsert(u => u.InvitedBy, invitedBy),
                                            new UpdateOptions
                                            {
                                                IsUpsert = true
                                            },
                                            ct);
                                        break;
                                    }
                            }
                        }

                        if (separated.Length != 1)
                            return;

                        {
                            var t1 = Task.Run(async () =>
                            {
                                await _bot.SendTextMessageAsync(userId, _config["welcome"], ParseMode.Markdown, cancellationToken: ct);
                                await _bot.SendTextMessageAsync(userId, _config["motivation"], ParseMode.Markdown, cancellationToken: ct,
                                    replyMarkup: new ReplyKeyboardMarkup(new KeyboardButton(_config["beginning"])) { ResizeKeyboard = true });
                            }, ct);
                            var t2 = _context.Users.UpdateOneAsync(u => u.Id == userId, Builders<Person>.Update
                                .SetOnInsert(u => u.Status, "begin").SetOnInsert(u => u.ActiveTask, ObjectId.Empty),
                                new UpdateOptions
                                {
                                    IsUpsert = true
                                },
                                ct);

                            await Task.WhenAll(t1, t2);
                        }
                        break;
                    }

                // Начинаем
                case { Text: { } t } when t == _config["beginning"] && status == "begin":
                    {

                        await ProcessNicknameChangingAsync(userId, ct);

                        break;
                    }

                // Ввод никнейма
                case { Text: { } t } when status == "nickname":
                    {
                        var user = await _stateManager.PushUsernameAsync(userId, t, ct);
                        if (user == null)
                        {
                            await _bot.SendTextMessageAsync(userId, _config["wrong_account"], ParseMode.Markdown,
                                cancellationToken: ct);
                            break;
                        }

                        var text = string.Format(_config["verify_account"], user.FullName);
                        var markup = new ReplyKeyboardMarkup(new[]
                        {
                            new[]
                            {
                                new KeyboardButton(_config["profile_ok"])
                            },
                            new[]
                            {
                                new KeyboardButton(_config["profile_bad"])
                            }
                        })
                        { ResizeKeyboard = true };

                        if (string.IsNullOrEmpty(user.ProfilePicture))
                        {
                            await _bot.SendTextMessageAsync(userId, text, ParseMode.Markdown, cancellationToken: ct,
                                replyMarkup: markup);
                        }
                        else
                        {
                            await _bot.SendPhotoAsync(userId, user.ProfilePicture, text, ParseMode.Markdown, cancellationToken: ct,
                                replyMarkup: markup);
                        }

                        break;
                    }

                // Отмена
                case { Text: { } t } when t == _config["cancel"] && (status == "password" || status == "nickname" || status == "media-link"):
                    {
                        var rulesAccepted = await _context.Users.Find(u => u.Id == userId).Project(u => u.RulesAccepted).SingleAsync(ct);
                        if (!rulesAccepted)
                        {
                            var t1 = _bot.SendTextMessageAsync(userId, _config["welcome"], ParseMode.Markdown, cancellationToken: ct,
                                replyMarkup: new ReplyKeyboardMarkup(new KeyboardButton(_config["beginning"])) { ResizeKeyboard = true });
                            var t2 = SetStatusAsync("begin");
                            await Task.WhenAll(t1, t2);
                        }
                        else
                        {
                            var t1 = ProcessMenuAsync(userId, ct);
                            var t2 = SetStatusAsync("menu");
                            await Task.WhenAll(t1, t2);
                        }
                        break;
                    }

                // Ввод пароля
                case { Text: { } t } when status == "password":
                    {
                        var instaApi = await _stateManager.PushPasswordAsync(userId, t, ct);
                        if (instaApi == null)
                        {
                            var t1 = Task.Run(async () =>
                            {
                                await _bot.SendTextMessageAsync(userId, "Не удалось авторизоваться с указанными логином и паролем.", cancellationToken: ct);
                                await ProcessNicknameChangingAsync(userId, ct);
                            });
                            var t2 = _context.Users.UpdateOneAsync(u => u.Id == userId, Builders<Person>.Update
                                .Unset(u => u.Username).Unset(u => u.Password),
                                cancellationToken: ct);
                            await Task.WhenAll(t1, t2);
                            break;
                        }

                        var rulesAccepted = await _context.Users.Find(u => u.Id == userId).Project(u => u.RulesAccepted).SingleAsync(ct);
                        if (!rulesAccepted)
                        {
                            var t1 = _bot.SendTextMessageAsync(userId, _config["rules_text"], ParseMode.Markdown, cancellationToken: ct,
                                replyMarkup: new ReplyKeyboardMarkup(new KeyboardButton(_config["agree_rules"])) { ResizeKeyboard = true });
                            var t2 = SetStatusAsync("accept_rules");
                            await Task.WhenAll(t1, t2);
                        }
                        else
                        {
                            var t1 = ProcessMenuAsync(userId, ct);
                            var t2 = SetStatusAsync("menu");
                            await Task.WhenAll(t1, t2);
                        }
                        break;
                    }

                // Да, это мой профиль
                case { Text: { } t } when t == _config["profile_ok"] && status == "your_profile":
                    {
                        await ProcessPasswordChangingAsync(userId, ct);
                        break;
                    }

                // Нет, это не мой профиль
                case { Text: { } t } when t == _config["profile_bad"] && status == "your_profile":
                    {
                        var t1 = _bot.SendTextMessageAsync(userId, _config["nickname_again"], ParseMode.Markdown, cancellationToken: ct,
                            replyMarkup: new ReplyKeyboardMarkup(new KeyboardButton(_config["cancel"])) { ResizeKeyboard = true });
                        var t2 = SetStatusAsync("nickname");
                        await Task.WhenAll(t1, t2);
                        break;
                    }

                // Я согласен с правилами
                case { Text: { } t } when t == _config["agree_rules"] && status == "accept_rules":
                    {
                        var t1 = ProcessMenuAsync(userId, ct);
                        var t2 = _context.Users.UpdateOneAsync(u => u.Id == userId,
                            Builders<Person>.Update.Set(u => u.Status, "menu").Set(u => u.RulesAccepted, true),
                            cancellationToken: ct);
                        await Task.WhenAll(t1, t2);
                        break;
                    }

                // Задания
                case { Text: { } t } when t == _config["tasks"] && status == "menu":
                    {
                        var activeTask = await _context.Users.Find(u => u.Id == userId).Project(u => u.ActiveTask).FirstAsync(ct);
                        if (activeTask == ObjectId.Empty)
                        {
                            var task = await _context.Tasks.Find(t => t.OwnerId != userId)
                                .Project<InstagramTask>("{_id:1}")
                                .FirstOrDefaultAsync(ct);

                            if (task == default)
                            {
                                await _bot.SendTextMessageAsync(userId, "Заданий ещё нет :с", cancellationToken: ct);
                                return;
                            }

                            await _context.Users.UpdateOneAsync(u => u.Id == userId, Builders<Person>.Update
                                .Set(u => u.ActiveTask, activeTask = task.Id), cancellationToken: ct);
                        }

                        if (await CheckTaskAsync(userId, activeTask, ct))
                        {
                            var totalCrowns = await _context.Tasks.Find(t => t.Id == activeTask).Project(t => t.TotalCrowns).FirstAsync(ct);
                            await _context.Users.UpdateOneAsync(u => u.Id == userId && u.IsAwarded == 0,
                                Builders<Person>.Update.Set(u => u.IsAwarded, 1).Inc(u => u.Crowns, totalCrowns),
                                cancellationToken: ct);
                        }
                        break;
                    }

                // Мои задания
                case { Text: { } t } when t.StartsWith(_config["my_tasks"]) && status == "menu":
                    {
                        await _bot.SendTextMessageAsync(userId, _config["msg14"], ParseMode.Markdown, cancellationToken: ct,
                            replyMarkup: new InlineKeyboardMarkup(
                                new InlineKeyboardButton { Text = _config["b13"], CallbackData = "to-msg15" }));
                        break;
                    }

                // Личный кабинет
                case { Text: { } t } when t == _config["profile"] && status == "menu":
                    {
                        var keyboard = new[]
                        {
                            new[]
                            {
                                new InlineKeyboardButton { Text = _config["change_nickname"], CallbackData = "change nickname" },
                                new InlineKeyboardButton { Text = _config["rules_button"], CallbackData = "rules button" }
                            },
                            new[]
                            {
                                new InlineKeyboardButton { Text = _config["replenish_account"], CallbackData = "replenish account" }
                            },
                            new[]
                            {
                                new InlineKeyboardButton { Text = _config["enable_vip"], CallbackData = "enable vip" }
                            },
                            new[]
                            {
                                new InlineKeyboardButton { Text = _config["share_urlCode"], SwitchInlineQuery = ""}
                            },
                            new[]
                            {
                                new InlineKeyboardButton { Text = _config["admin"], CallbackData= "admin panel"}
                            }
                        };
                        await _bot.SendTextMessageAsync(userId, _config["profile_info"], ParseMode.Markdown, cancellationToken: ct,
                            replyMarkup: new InlineKeyboardMarkup(keyboard));
                        break;
                    }

                // Получи бонусные 👑
                case { Text: { } t } when t == _config["bonuses"] && status == "menu":
                    {

                        break;
                    }

                case { Text: "/clear" } when _env.IsDevelopment():
                    {
                        var t1 = _context.Users.DeleteOneAsync(u => u.Id == userId, ct);
                        var t2 = _bot.SendTextMessageAsync(userId, "Информация успешно очищена");
                        break;
                    }

                case { Text: "/crowns" }:
                    {
                        await _context.Users.UpdateOneAsync(u => u.Id == userId, Builders<Person>.Update.Inc(u => u.Crowns, 1000));
                        break;
                    }

                case { Text: "/menu" } when _env.IsDevelopment() && status == "menu":
                    {
                        await ProcessMenuAsync(userId, ct);
                        break;
                    }

                case { Text: var t } when status == "media-link":
                    {
                        if (!Uri.TryCreate(t, UriKind.Absolute, out var mediaUrl))
                        {
                            await _bot.SendTextMessageAsync(userId, "Вы прислали не ссылку. Попробуйте ещё раз.", ParseMode.Markdown,
                                cancellationToken: ct);
                            return;
                        }

                        var mediaId = await _adminApi.MediaProcessor.GetMediaIdFromUrlAsync(mediaUrl);
                        if (!mediaId.Succeeded)
                            return;

                        IResult<InstaMedia> media;
                        if (!mediaId.Succeeded || !(media = await _adminApi.MediaProcessor.GetMediaByIdAsync(mediaId.Value)).Succeeded)
                        {
                            await _bot.SendTextMessageAsync(userId, "Не удалось найти указанный пост.", ParseMode.Markdown,
                                cancellationToken: ct);
                            return;
                        }

                        var crowns = await _context.Users.Find(u => u.Id == userId).Project(u => u.Crowns)
                            .FirstOrDefaultAsync(ct);
                        if (crowns < 20)
                        {
                            await _bot.SendTextMessageAsync(userId, _config["msg16"], ParseMode.Markdown, cancellationToken: ct,
                                replyMarkup: new InlineKeyboardMarkup(new InlineKeyboardButton { Text = "Поменять валюту", CallbackData = "to-msg11" }));
                        }
                        else
                        {
                            var keyboard = new[]
                            {
                                new[]
                                {
                                    new InlineKeyboardButton { Text = "✅ | ♥️ + 💬 - 20👑", CallbackData = "ignore" }
                                },
                                new[]
                                {
                                    new InlineKeyboardButton { Text = "👀 - 15👑", CallbackData = "watch-story" }
                                },
                                new[]
                                {
                                    new InlineKeyboardButton { Text = "📌 - 15👑", CallbackData = "save-to-bookmarks" }
                                },
                                new[]
                                {
                                    new InlineKeyboardButton { Text = "👤 - 20👑", CallbackData = "subscribe" }
                                },
                                new[]
                                {
                                    new InlineKeyboardButton { Text = "Сохранить", CallbackData = "save" }
                                }
                            };

                            var t1 = _bot.SendTextMessageAsync(userId, string.Format(_config["msg17"], 20), ParseMode.Markdown, cancellationToken: ct,
                                replyMarkup: new InlineKeyboardMarkup(keyboard));
                            var t2 = _context.Users.UpdateOneAsync(u => u.Id == userId, Builders<Person>.Update
                                .Set(u => u.Subscribe, 0).Set(u => u.SaveToBookmarks, 0).Set(u => u.WatchStory, 0)
                                .Set(u => u.MediaId, mediaId.Value).Set(u => u.Url, mediaUrl.ToString()),
                                cancellationToken: ct);
                        }
                        break;
                    }

                case { SuccessfulPayment: { } sp }:
                    {
                        var product = await _context.Products.Find(p => p.Id == sp.InvoicePayload).FirstOrDefaultAsync();
                        if (product == default)
                        {
                            await _bot.SendTextMessageAsync(userId, "Не удалось найти товар, за который Вы только что заплатили. Пожалуйста, " +
                                "напишите администратору, показав размещённый выше чек, и Ваша покупка будет восстановлена.",
                                ParseMode.Markdown, cancellationToken: ct);
                            break;
                        }

                        await _bot.SendTextMessageAsync(userId, _config["text_payment_was_successful"], ParseMode.Markdown, cancellationToken: ct);

                        var stage1 = new JsonPipelineStageDefinition<Person, Person>(
                            $"{{ $set: {{ VipTo: {{ $add: [ \"$VipTo\", 86400000 * {product.Items.Sum(it => it.VipDays)} ] }} }} }}");
                        var stage2 = new JsonPipelineStageDefinition<Person, Person>(
                            $"{{ $set: {{ Crowns: {{ $add: [ \"$Crowns\", {product.Items.Sum(it => it.Crowns)} ] }} }} }}");
                        PipelineDefinition<Person, Person> pipelineDefinition = new IPipelineStageDefinition[] { stage1, stage2 };

                        await _context.Users.UpdateOneAsync(u => u.Id == userId,
                            new PipelineUpdateDefinition<Person>(pipelineDefinition),
                            cancellationToken: ct);
                        break;
                    }
            }
        }

        private Task ProcessMenuAsync(int userId, CancellationToken ct)
        {
            var keyboard = new[]
            {
                new[]
                {
                    new KeyboardButton(_config["tasks"]),
                    new KeyboardButton(_config["my_tasks"])
                },
                new[]
                {
                    new KeyboardButton(_config["profile"]),
                    new KeyboardButton(_config["bonuses"])
                },
                new[]
                {
                    new KeyboardButton(_config["rules_button"])
                }
            };
            return _bot.SendTextMessageAsync(userId, "🏰", ParseMode.Markdown, cancellationToken: ct,
                replyMarkup: new ReplyKeyboardMarkup(keyboard) { ResizeKeyboard = true });
        }

        private Task ProcessNicknameChangingAsync(int userId, CancellationToken ct)
        {
            var t1 = _bot.SendTextMessageAsync(userId, _config["nickname"], ParseMode.Markdown, cancellationToken: ct,
                replyMarkup: new ReplyKeyboardMarkup(new KeyboardButton(_config["cancel"])) { ResizeKeyboard = true });

            var t2 = _context.Users.UpdateOneAsync(u => u.Id == userId, Builders<Person>.Update.Set(u => u.Status, "nickname"),
                cancellationToken: ct);
            return Task.WhenAll(t1, t2);
        }

        private Task ProcessPasswordChangingAsync(int userId, CancellationToken ct)
        {
            var t1 = _bot.SendTextMessageAsync(userId, _config["password"], ParseMode.Markdown, cancellationToken: ct,
                replyMarkup: new ReplyKeyboardMarkup(new KeyboardButton(_config["cancel"])) { ResizeKeyboard = true });

            var t2 = _context.Users.UpdateOneAsync(u => u.Id == userId, Builders<Person>.Update.Set(u => u.Status, "password"),
               cancellationToken: ct);

            return Task.WhenAll(t1, t2);
        }
    }
}
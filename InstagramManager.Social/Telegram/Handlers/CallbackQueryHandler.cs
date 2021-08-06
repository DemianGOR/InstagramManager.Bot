using InstagramManager.Data.Enums;
using InstagramManager.Data.Models;
using InstagramManager.Social.Instagram.Extensions;
using InstagramManager.Social.Models;
using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.Payments;
using Telegram.Bot.Types.ReplyMarkups;

namespace InstagramManager.Social.Telegram.Handlers
{
    public partial class UpdateHandler
    {
        private async Task HandleCallbackQuery(CallbackQuery q, CancellationToken ct)
        {
            var userId = q?.From?.Id ?? 0;
            if (userId == 0)
                return;

            Task SetStatusAsync(string newStatus)
            {
                return _context.Users.UpdateOneAsync(u => u.Id == userId, Builders<Person>.Update
                    .Set(u => u.Status, newStatus), cancellationToken: ct);
            }

            async Task EditKeyboardAsync(UpdateDefinition<Person> update)
            {
                var user = await _context.Users.FindOneAndUpdateAsync<Person, Person>(u => u.Id == userId, update,
                    new FindOneAndUpdateOptions<Person, Person>
                    {
                        IsUpsert = false,
                        ReturnDocument = ReturnDocument.After,
                        Projection = "{SaveToBookmarks:1,WatchStory:1,Subscribe:1,_id:0}"
                    },
                    ct);

                var price = 20;
                if (user.WatchStory == 1)
                    price += 15;
                if (user.Subscribe == 1)
                    price += 20;
                if (user.SaveToBookmarks == 1)
                    price += 15;

                var keyboard = new[]
                {
                    new[]
                    {
                        new InlineKeyboardButton { Text = "✅ | ♥️ + 💬 - 20👑", CallbackData = "ignore" }
                    },
                    new[]
                    {
                        new InlineKeyboardButton { Text = $"{(user.WatchStory == 1 ? "✅ | " : "")}👀 - 15👑", CallbackData = "watch-story" }
                    },
                    new[]
                    {
                        new InlineKeyboardButton { Text = $"{(user.SaveToBookmarks == 1 ? "✅ | " : "")}📌 - 15👑", CallbackData = "save-to-bookmarks" }
                    },
                    new[]
                    {
                        new InlineKeyboardButton { Text = $"{(user.Subscribe == 1 ? "✅ | " : "")}👤 - 20👑", CallbackData = "subscribe" }
                    },
                    new[]
                    {
                        new InlineKeyboardButton { Text = "Сохранить", CallbackData = "save" }
                    }
                };

                await _bot.EditMessageTextAsync(userId, q.Message.MessageId, string.Format(_config["msg17"], price), ParseMode.Markdown,
                    cancellationToken: ct, replyMarkup: new InlineKeyboardMarkup(keyboard));
            }

            switch (q)
            {
                //case { Data: "admin panel" }:
                //    {
                //        var result = await _context.Admin.Find(p => p.IdTelegram.ToString() == q.Id).FirstOrDefaultAsync();
                //        if(result != null)
                //        {

                //        }
                //        break;
                //    }
                case { Data: "to-msg15" }:
                    {
                        var t1 = _bot.SendTextMessageAsync(userId, _config["msg15"], cancellationToken: ct,
                            replyMarkup: new ReplyKeyboardMarkup(new KeyboardButton("Отмена")) { ResizeKeyboard = true });
                        var t2 = SetStatusAsync("media-link");
                        var t3 = _bot.EditMessageReplyMarkupAsync(userId, q.Message.MessageId, cancellationToken: ct);
                        await Task.WhenAll(t1, t2, t3);
                        break;
                    }

                case { Data: "to-msg11" }:
                    {
                        try
                        {
                            await _bot.AnswerCallbackQueryAsync(q.Id);
                        }
                        catch
                        {
                            // silent
                        }

                        // TODO
                        break;
                    }

                case { Data: "ignore" }:
                    {
                        try
                        {
                            await _bot.AnswerCallbackQueryAsync(q.Id);
                        }
                        catch
                        {
                            // silent
                        }

                        break;
                    }

                case { Data: "save-to-bookmarks" }:
                    {
                        var update = Builders<Person>.Update.BitwiseXor(u => u.SaveToBookmarks, 1);
                        await EditKeyboardAsync(update);
                        break;
                    }

                case { Data: "subscribe" }:
                    {
                        var update = Builders<Person>.Update.BitwiseXor(u => u.Subscribe, 1);
                        await EditKeyboardAsync(update);
                        break;
                    }

                case { Data: "watch-story" }:
                    {
                        var update = Builders<Person>.Update.BitwiseXor(u => u.WatchStory, 1);
                        await EditKeyboardAsync(update);
                        break;
                    }

                case { Data: "save" }:
                    {
                        var user = await _context.Users.Find(u => u.Id == userId).FirstOrDefaultAsync();
                        if (user == default)
                            return;

                        int price = 20;

                        var mode = InstagramTaskMode.LikeAndComment;
                        if (user.Subscribe == 1)
                        {
                            mode |= InstagramTaskMode.Subscribing;
                            price += 20;
                        }
                        if (user.SaveToBookmarks == 1)
                        {
                            mode |= InstagramTaskMode.SavingPost;
                            price += 15;
                        }
                        if (user.WatchStory == 1)
                        {
                            mode |= InstagramTaskMode.WatchingStory;
                            price += 15;
                        }

                        if (user.Crowns < price)
                        {
                            await _bot.AnswerCallbackQueryAsync(q.Id, $"У тебя {user.Crowns}👑, а для покупки нужно {price}👑", true,
                                cancellationToken: ct);
                        }

                        var newTask = new InstagramTask
                        {
                            Id = ObjectId.GenerateNewId(),
                            MediaId = user.MediaId,
                            Url = user.Url,
                            Mode = mode,
                            OwnerId = userId
                        };
                        await _context.Tasks.InsertOneAsync(newTask);

                        await _context.Users.UpdateOneAsync(u => u.Id == userId, Builders<Person>.Update.Unset(u => u.MediaId)
                            .Unset(u => u.SaveToBookmarks).Unset(u => u.Subscribe).Unset(u => u.WatchStory)
                            .Inc(u => u.Crowns, -price).Unset(u => u.Url), cancellationToken: ct);

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

                        var t1 = _bot.SendTextMessageAsync(userId, "Задание успешно отправлено и очень скоро его начнут выполнять!", ParseMode.Markdown, cancellationToken: ct,
                            replyMarkup: new ReplyKeyboardMarkup(keyboard) { ResizeKeyboard = true });
                        var t2 = SetStatusAsync("menu");
                        var t3 = _bot.EditMessageReplyMarkupAsync(userId, q.Message.MessageId, cancellationToken: ct);
                        await Task.WhenAll(t1, t2, t3);
                        break;
                    }

                case { Data: "free-vip" }:
                    {
                        var stage1 = new JsonPipelineStageDefinition<Person, Person>(
                            "{ $set: { FreeVipUsed: true } }");
                        var stage2 = new JsonPipelineStageDefinition<Person, Person>(
                            "{ $set: { VipTo: { $add: [ \"$VipTo\", 86400000 ] } } }");
                        PipelineDefinition<Person, Person> pipelineDefinition = new IPipelineStageDefinition[] { stage1, stage2 };

                        var updateResult = await _context.Users.UpdateOneAsync(u => u.Id == userId && !u.FreeVipUsed,
                            new PipelineUpdateDefinition<Person>(pipelineDefinition),
                            cancellationToken: ct);
                        if (updateResult.IsAcknowledged && updateResult.ModifiedCount == 1L)
                        {
                            try
                            {
                                await _bot.AnswerCallbackQueryAsync(q.Id, "Бесплатная VIP автоматизация активирована на 1 день", true,
                                    cancellationToken: ct);
                            }
                            catch
                            {
                                // silent
                            }

                            var products = await _context.Products.Find(FilterDefinition<Product>.Empty)
                                .Project<Product>("{Title:1}")
                                .ToListAsync(ct);

                            var keyboard = products.Select(p => new[]
                            {
                                new InlineKeyboardButton { Text = p.Title, CallbackData = $"product {p.Id}" }
                            });
                            await _bot.EditMessageReplyMarkupAsync(userId, q.Message.MessageId, cancellationToken: ct,
                                replyMarkup: new InlineKeyboardMarkup(keyboard));

                            return;
                        }

                        try
                        {
                            await _bot.AnswerCallbackQueryAsync(q.Id, cancellationToken: ct);
                        }
                        catch
                        {
                            // silent
                        }
                        break;
                    }

                case { Data: { } d } when d.StartsWith("product"):
                    {
                        var separated = d.Split(' ');
                        if (separated.Length < 2)
                        {
                            break;
                        }

                        var productId = separated[1];
                        var product = await _context.Products.Find(p => p.Id == productId).FirstOrDefaultAsync();
                        if (product == default)
                        {
                            try
                            {
                                await _bot.AnswerCallbackQueryAsync(q.Id, "Тариф не найден");
                            }
                            catch
                            {
                                // silent
                            }
                            break;
                        }

                        try
                        {
                            await _bot.SendInvoiceAsync(userId,
                                product.Title,
                                product.Description,
                                product.Id,
                                _config["payment_provider"],
                                $"pr-{product.Id}",
                                _config["payment_currency"],
                                product.Items.Select(it => new LabeledPrice { Label = it.Label, Amount = it.Price }),
                                cancellationToken: ct);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex);
                        }
                        break;
                    }

                case { Data: "next task", Id: var qId, Message: { MessageId: var msgId } }:
                    {
                        PipelineDefinition<InstagramTask, InstagramTask> definition = new IPipelineStageDefinition[]
                        {
                            PipelineStageDefinitionBuilder.Match<InstagramTask>(it => it.OwnerId != userId),
                            PipelineStageDefinitionBuilder.Lookup<InstagramTask, PassedTask, PassedTask, List<PassedTask>, AggregatedTask>(
                                _context.PassedTasks,
                                new BsonDocument("task_id", "$_id"),
                                new IPipelineStageDefinition[]
                                {
                                    PipelineStageDefinitionBuilder.Match(
                                        Builders<PassedTask>.Filter.Eq(pt => pt.UserId, userId) &
                                        Builders<PassedTask>.Filter.Eq(nameof(PassedTask.PassedTaskId), "$$task_id"))
                                },
                                at => at.PassedTasks),
                            new JsonPipelineStageDefinition<AggregatedTask, AggregatedTask>(
                                "{ $set: { PassedTasksCount: { $size: \"$PassedTasks\" } } }"),
                            PipelineStageDefinitionBuilder.Match<AggregatedTask>(at => at.PassedTasksCount == 0),
                            PipelineStageDefinitionBuilder.Project<AggregatedTask, InstagramTask>(
                                Builders<AggregatedTask>.Projection.Exclude(at => at.PassedTasksCount).Exclude(at => at.PassedTasks))
                        };

                        var nextTask = await _context.Tasks.Aggregate(definition, cancellationToken: ct).FirstOrDefaultAsync(ct);
                        
                        if (nextTask == default)
                        {
                            try
                            {
                                await _bot.AnswerCallbackQueryAsync(qId,
                                    "Нет доступных для выполнения задач. Пожалуйста, попробуйте позже.",
                                    true);
                            }
                            catch
                            {
                                // silent
                            }
                            break;
                        }

                        await _context.Users.UpdateOneAsync(u => u.Id == userId,
                            Builders<Person>.Update.Set(u => u.IsAwarded, 0)
                            .Set(u => u.ActiveTask, nextTask.Id));

                        if (await CheckTaskAsync(userId, nextTask.Id, ct, msgId))
                        {
                            var totalCrowns = await _context.Tasks.Find(t => t.Id == nextTask.Id).Project(t => t.TotalCrowns).FirstAsync(ct);
                            if ((await _context.Users.UpdateOneAsync(u => u.Id == userId && u.IsAwarded == 0,
                                Builders<Person>.Update.Set(u => u.IsAwarded, 1).Inc(u => u.Crowns, totalCrowns),
                                cancellationToken: ct)).ModifiedCount == 1L)
                            {
                                try
                                {
                                    await _bot.AnswerCallbackQueryAsync(qId, $"Вам начислено {totalCrowns}👑", true, cancellationToken: ct);
                                }
                                catch
                                {
                                    // silent
                                }
                            }
                        }
                        break;
                    }

                case { Data: "check task", Message: { MessageId: var msgId }, Id: { } qId }:
                    {
                        var activeTask = await _context.Users.Find(u => u.Id == userId).Project(u => u.ActiveTask).FirstAsync(ct);
                        if (await CheckTaskAsync(userId, activeTask, ct, msgId))
                        {
                            var totalCrowns = await _context.Tasks.Find(t => t.Id == activeTask).Project(t => t.TotalCrowns).FirstAsync(ct);
                            if ((await _context.Users.UpdateOneAsync(u => u.Id == userId && u.IsAwarded == 0,
                                Builders<Person>.Update.Set(u => u.IsAwarded, 1).Inc(u => u.Crowns, totalCrowns),
                                cancellationToken: ct)).ModifiedCount == 1L)
                            {
                                try
                                {
                                    await _bot.AnswerCallbackQueryAsync(qId, $"Вам начислено {totalCrowns}👑", true, cancellationToken: ct);
                                }
                                catch
                                {
                                    // silent
                                }
                            }
                        }
                        break;
                    }

                // Правила
                case { Data: "rules button" }:
                    {
                        await _bot.SendTextMessageAsync(userId, _config["rules_text"], ParseMode.Markdown, cancellationToken: ct);
                        break;
                    }

                // Пополнить счет
                case { Data: "replenish account" }:
                    {
                        var products = await _context.Products.Find(FilterDefinition<Product>.Empty)
                            .Project<Product>("{Title:1}")
                            .ToListAsync(ct);

                        var keyboard = products.Select(p => new[]
                        {
                            new InlineKeyboardButton { Text = p.Title, CallbackData = $"product {p.Id}" }
                        });

                        var user = await _context.Users.Find(u => u.Id == userId).Project<Person>("{FreeVipUsed:1,_id:0}")
                            .FirstOrDefaultAsync();
                        if (user == default)
                        {
                            return;
                        }

                        if (!user.FreeVipUsed)
                        {
                            keyboard = keyboard.Prepend(new[]
                            {
                                new InlineKeyboardButton
                                {
                                    Text = "Попробовать VIP автоматизацию - 1 день бесплатно",
                                    CallbackData = "free-vip"
                                }
                            });
                        }

                        await _bot.SendTextMessageAsync(userId, "Описание тарифов", ParseMode.Markdown, cancellationToken: ct,
                            replyMarkup: new InlineKeyboardMarkup(keyboard));
                        break;
                    }

                // Подключить VIP. Выполнение заданий на автомате
                case { Data: "enable vip" }:
                    {

                        break;
                    }

                // Сменить инстаграмм
                case { Data: "change nickname" }:
                    {
                        await ProcessNicknameChangingAsync(userId, ct);
                        break;
                    }
            }
        }

        private async Task<bool> CheckTaskAsync(int userId, ObjectId activeTask, CancellationToken ct, int messageId = 0)
        {
            var notPassed = false;
            var actions = new List<string>(5);

            var taskUrl = await _context.Tasks.Find(t => t.Id == activeTask).Project(t => t.Url).FirstAsync();

            if (messageId == 0)
            {
                var msgText = BuildTaskMessage(new[] { "🔄 <i>Идет проверка задания...</i>" }, taskUrl);
                var msg = await _bot.SendTextMessageAsync(userId, msgText, ParseMode.Html, cancellationToken: ct);
                messageId = msg.MessageId;
            }

            await foreach (var result in (await _stateManager.GetInstaApiForUserAsync(userId, ct)).CheckIfTaskPassedAsync(_context, userId, activeTask, ct))
            {
                var toBreak = false;

                switch (result)
                {
                    case { Succeeded: var succeeded, IsPassed: var isPassed, Name: { } name }:
                        {
                            actions.Add(string.Format("{0} {1}", !succeeded ? "🅾️" : isPassed ? "✅" : "☑️", name));

                            if (!isPassed)
                                notPassed = true;
                            break;
                        }

                    case { InternalError: true }:
                        {
                            actions.Add("🅾️ Произошла ошибка на сервере, невозможно выполнить проверку. Попробуйте снова позже.");
                            toBreak = true;
                            break;
                        }

                    case { MediaNotFound: true }:
                        {
                            actions.Add("🅾️ Не удалось найти пост. Пожалуйста, переключитесь на следующее задание.");
                            notPassed = true;
                            toBreak = true;
                            break;
                        }

                    case { TaskNotFound: true }:
                        {
                            actions.Add("🅾️ Не удалось найти задание. Пожалуйста, переключитесь на следующее задание.");
                            notPassed = true;
                            toBreak = true;
                            break;
                        }
                }

                var msgText = BuildTaskMessage(actions, taskUrl);
                await _bot.EditMessageTextAsync(userId, messageId, msgText, ParseMode.Html, cancellationToken: ct);

                if (toBreak)
                    break;
            }

            IEnumerable<InlineKeyboardButton[]> keyboard = new[]
            {
                new[]
                {
                    new InlineKeyboardButton { CallbackData = "next task", Text = "Следующее задание" }
                }
            };

            if (notPassed)
            {
                keyboard = keyboard.Prepend(
                    new[] { new InlineKeyboardButton { Text = "Проверить", CallbackData = "check task" } });
            }

            await _bot.EditMessageReplyMarkupAsync(userId, messageId, new InlineKeyboardMarkup(keyboard), ct);

            return !notPassed;
        }

        private static string BuildTaskMessage(IEnumerable<string> actions, string taskUrl)
        {
            var msg = new StringBuilder();
            msg.Append("Выполни задания:\n");
            msg.AppendJoin('\n', actions);
            msg.AppendFormat("\n\n<a href=\"{0}\">ССЫЛКА</a>\n\n", taskUrl);
            msg.Append("⚠️ Когда выполнишь все задания - нажми кнопку «Проверить».\n\n");
            msg.Append("Если при проверке серая галочка не загорается зеленой, выполни задание заново, при ошибке на подписку - отпишись и подпишись на человека заново.\n");
            msg.Append("✅ - выполненные задания\n");
            msg.Append("☑️ - не выполнено задание");
            return msg.ToString();
        }
    }
}

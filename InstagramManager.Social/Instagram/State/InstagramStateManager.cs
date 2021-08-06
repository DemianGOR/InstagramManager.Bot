using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using InstagramApiSharp.API;
using InstagramApiSharp.API.Builder;
using InstagramApiSharp.Classes;
using InstagramApiSharp.Classes.Models;
using InstagramApiSharp.Logger;
using InstagramManager.Data.Context;
using InstagramManager.Data.Models;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

namespace InstagramManager.Social.Instagram.State
{
    public sealed class InstagramStateManager : IInstagramStateManager, IInstagramAdminStateManager
    {
        private readonly Dictionary<int, IInstaApi> _apis = new Dictionary<int, IInstaApi>(100);
        private readonly DataContext _context;
        private readonly HttpClient _http;
        private readonly IOptionsMonitor<InstagramOptions> _options;
        private readonly ITelegramBotClient _bot;

        public IInstaApi AdminApi { get; }

        public InstagramStateManager(DataContext context, IInstaApiHttpContainer httpContainer, IInstaApi adminApi,
            IOptionsMonitor<InstagramOptions> options, ITelegramBotClient bot)
        {
            _context = context;
            _http = httpContainer.Instance;
            AdminApi = adminApi;
            _options = options;
            _bot = bot;
        }

        public async ValueTask<IInstaApi> GetInstaApiForUserAsync(int userId, CancellationToken ct)
        {
            lock (_apis)
            {
                if (_apis.TryGetValue(userId, out var api))
                    return api;
            }

            var user = await _context.Users.Find(u => u.Id == userId).FirstOrDefaultAsync(ct);
            if (user == default)
                return null;

            return await ProcessInstaApiAsync(user, ct);
        }

        public IInstaApi GetAdminApi()
        {
            return AdminApi;
        }

        public async Task<IInstaApi> PushPasswordAsync(int userId, string password, CancellationToken ct)
        {
            var user = await _context.Users.FindOneAndUpdateAsync<Person>(u => u.Id == userId,
                Builders<Person>.Update.Set(u => u.Password, password),
                new FindOneAndUpdateOptions<Person, Person>
                {
                    IsUpsert = false,
                    ReturnDocument = ReturnDocument.After
                },
                cancellationToken: ct);

            return await ProcessInstaApiAsync(user, ct);
        }

        public async Task<InstaUser> PushUsernameAsync(int userId, string username, CancellationToken ct)
        {
            var userResult = await AdminApi.UserProcessor.GetUserAsync(username);
            if (!userResult.Succeeded || !(userResult.Value is InstaUser user))
                return null;

            await _context.Users.UpdateOneAsync(u => u.Id == userId,
                Builders<Person>.Update.Set(u => u.Username, username)
                    .Set(u => u.Status, "your_account"),
                new UpdateOptions
                {
                    IsUpsert = false
                },
                cancellationToken: ct);

            return user;
        }

        private async Task<IInstaApi> ProcessInstaApiAsync(Person user, CancellationToken ct)
        {
            var instaApi = InstaApiBuilder.CreateBuilder()
                .SetRequestDelay(RequestDelay.FromSeconds(1, 3))
                .UseHttpClient(_http)
                .UseLogger(new DebugLogger(LogLevel.Exceptions))
                .Build();

            await AuthorizeAsync(user.Id, instaApi, user, ct);

            lock (_apis)
            {
                _apis[user.Id] = instaApi;
            }

            return instaApi;
        }

        private async Task<bool> AuthorizeAsync(int userId, IInstaApi api, Person person, CancellationToken ct)
        {
            var optionsValue = _options.CurrentValue;
            var pathToFile = Path.Combine(Directory.GetCurrentDirectory(), string.Format(optionsValue.StateFilePrefix, userId));

            if (File.Exists(pathToFile))
            {
                await using var file = File.OpenRead(pathToFile);
                var stateData = await JsonSerializer.DeserializeAsync<StateData>(file, cancellationToken: ct);
                api.LoadStateDataFromObject(stateData);

                var currentUserResult = await api.GetCurrentUserAsync();
                if (currentUserResult.Succeeded)
                    return true;
            }

            switch (person.LoginStatus)
            {
                case null:
                    {
                        api.SetUser(person.Username, person.Password);
                        var loginResult = await api.LoginAsync();

                        try
                        {
                            switch (loginResult.Value)
                            {
                                case InstaLoginResult.Success:
                                    return true;

                                case InstaLoginResult.ChallengeRequired:
                                    {
                                        var challengeMethodResult = await api.GetChallengeRequireVerifyMethodAsync();
                                        if (!challengeMethodResult.Succeeded)
                                            break;

                                        if (challengeMethodResult.Value.SubmitPhoneRequired)
                                        {
                                            var t1 = _bot.SendTextMessageAsync(userId, "Инстаграмм требует пройти проверку для авторизации. " +
                                                "Введите Ваш номер телефона, который привязан к Вашему аккаунту, включая код страны.", cancellationToken: ct);
                                            var t2 = _context.Users.UpdateOneAsync(u => u.Id == userId, Builders<Person>.Update
                                                .Set(u => u.LoginStatus, "phone"), cancellationToken: ct);
                                            await Task.WhenAll(t1, t2);
                                        }
                                        else
                                        {
                                            var t1 = _bot.SendTextMessageAsync(userId, "Инстаграмм требует пройти проверку для авторизации. " +
                                                "Выберите способ подтверждения:", cancellationToken: ct,
                                                replyMarkup: new ReplyKeyboardMarkup(new[]
                                                {
                                                    new[] { new KeyboardButton("По СМС") },
                                                    new[] { new KeyboardButton("По email") }
                                                }));
                                            var t2 = _context.Users.UpdateOneAsync(u => u.Id == userId, Builders<Person>.Update
                                                .Set(u => u.LoginStatus, "verify-method"), cancellationToken: ct);
                                            await Task.WhenAll(t1, t2);
                                        }
                                        break;
                                    }

                                case InstaLoginResult.TwoFactorRequired:
                                    {
                                        break;
                                    }
                            }
                        }
                        catch (Exception)
                        {
                            // TODO
                        }
                        finally
                        {
                            if (api.IsUserAuthenticated)
                            {
                                await using var file = File.Open(pathToFile, FileMode.Create, FileAccess.Write, FileShare.None);
                                var stateData = api.GetStateDataAsObject();
                                await JsonSerializer.SerializeAsync(file, stateData, cancellationToken: CancellationToken.None);
                            }
                        }
                        break;
                    }

                case "phone":
                    {
                        var result = await api.SubmitPhoneNumberForChallengeRequireAsync(person.LoginData);
                        if (!result.Succeeded)
                        {
                            await _bot.SendTextMessageAsync(userId, "Номер неверный. Повторите.", cancellationToken: ct);
                            break;
                        }

                        var t1 = _bot.SendTextMessageAsync(userId, "Инстаграмм требует пройти проверку для авторизации. " +
                            "Выберите способ подтверждения:", cancellationToken: ct,
                            replyMarkup: new ReplyKeyboardMarkup(new[]
                            {
                                new[] { new KeyboardButton("По СМС") },
                                new[] { new KeyboardButton("По email") }
                            }));
                        var t2 = _context.Users.UpdateOneAsync(u => u.Id == userId, Builders<Person>.Update
                            .Set(u => u.LoginStatus, "verify-method"), cancellationToken: ct);
                        await Task.WhenAll(t1, t2);
                        break;
                    }

                case "verify-method":
                    {
                        await _bot.SendTextMessageAsync(userId, $"Код отправлен по {person.LoginData}. Перешлите его боту.");
                        switch (person.LoginData)
                        {
                            case "sms":
                                {
                                    var smsResult = await api.RequestVerifyCodeToSMSForChallengeRequireAsync();
                                    if (!smsResult.Succeeded)
                                    {
                                        var t1 = _bot.SendTextMessageAsync(userId, "Код неверный. Повторите авторизацию.", cancellationToken: ct);
                                        var t2 = _context.Users.UpdateOneAsync(u => u.Id == userId, Builders<Person>.Update
                                            .Set(u => u.LoginStatus, "verify-method").Unset(u => u.LoginData), cancellationToken: ct);
                                        var t3 = api.ResetChallengeRequireVerifyMethodAsync();
                                    }
                                    break;
                                }

                            case "email":
                                {
                                    break;
                                }
                        }
                        break;
                    }

                case "code":
                    {
                        var codeResult = await api.VerifyCodeForChallengeRequireAsync(person.LoginData);
                        if (!codeResult.Succeeded || codeResult.Value != InstaLoginResult.Success)
                        {
                            var t1 = api.ResetChallengeRequireVerifyMethodAsync();
                            var t2 = _bot.SendTextMessageAsync(userId, "Код неверный.");
                            var t3 = _context.Users.UpdateOneAsync(u => u.Id == userId, Builders<Person>.Update
                                .Unset(u => u.LoginStatus), cancellationToken: ct);
                            await Task.WhenAll(t1, t2, t3);
                        }
                        else
                        {
                            var t1 = _bot.SendTextMessageAsync(userId, "Авторизация прошла успешно.");
                            var t2 = _context.Users.UpdateOneAsync(u => u.Id == userId, Builders<Person>.Update
                                .Unset(u => u.LoginStatus), cancellationToken: ct);
                            return true;
                        }
                        break;
                    }
            }
            return false;
        }

        Task<bool> IInstagramAdminStateManager.ProcessAdminAsync(Person adminAccount, CancellationToken ct)
        {
            var opts = _options.CurrentValue;
            return AuthorizeAsync(opts.AdminId, AdminApi, adminAccount, ct);
        }
    }
}

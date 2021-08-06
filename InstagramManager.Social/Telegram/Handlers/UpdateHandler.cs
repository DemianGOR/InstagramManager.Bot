using InstagramApiSharp.API;
using InstagramManager.Data.Context;
using InstagramManager.Social.Instagram;
using InstagramManager.Social.Instagram.State;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Extensions.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InlineQueryResults;

namespace InstagramManager.Social.Telegram.Handlers
{
    public sealed partial class UpdateHandler : IUpdateHandler
    {
        private readonly ILogger<UpdateHandler> _logger;
        private readonly ITelegramBotClient _bot;
        private readonly DataContext _context;
        private readonly IConfiguration _config;
        private readonly IInstaApi _adminApi;
        private readonly IHostEnvironment _env;
        private readonly IInstagramStateManager _stateManager;
        private readonly IOptions<InstagramOptions> _instaOptions;
        public UpdateHandler(ILogger<UpdateHandler> logger,
            ITelegramBotClient bot,
            DataContext context,
            IConfiguration config,
            IInstaApi adminApi,
            IHostEnvironment env,
            IInstagramStateManager stateManager,
            IOptions<InstagramOptions> instaOptions)
        {
            _logger = logger;
            _bot = bot;
            _context = context;
            _config = config;
            _adminApi = adminApi;
            _env = env;
            _stateManager = stateManager;
            _instaOptions = instaOptions;
        }

        public UpdateType[] AllowedUpdates => new[] { UpdateType.CallbackQuery, UpdateType.Message, UpdateType.PreCheckoutQuery, UpdateType.InlineQuery };

        public Task HandleError(Exception exception, CancellationToken cancellationToken)
        {
            _logger.LogError(exception, "Error occurred while handling the received update.");
            return Task.CompletedTask;
        }

        public async Task HandleUpdate(Update update, CancellationToken cancellationToken)
        {
            try
            {
                
                await (update switch
                {
                    { CallbackQuery: { } q } => HandleCallbackQuery(q, cancellationToken),
                    { Message: { } m } => HandleMessage(m, cancellationToken),
                    { PreCheckoutQuery: { } q } => HandlePreCheckoutQuery(q, cancellationToken),
                    { InlineQuery: { } q } => HandleInlineQuery(q, cancellationToken),
                    _ => Task.CompletedTask
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lol");
            }
        }
        private async Task HandleInlineQuery(InlineQuery q, CancellationToken ct)
        {
            // Если юзера нет в бд бота, для него промокод не может быть создан
            if (!await _context.Users.Find(u => u.Id == q.From.Id).AnyAsync(ct))
                return;

            // wtf, idk. не называй классы и неймспейсы одинаково
            GeneratorDeepURL.GeneratorDeepURL gn = new GeneratorDeepURL.GeneratorDeepURL(_context);
            var url = gn.GenerateUrlInviteCode(q.From.Id);
            InputTextMessageContent itmc = new InputTextMessageContent(url);

            try
            {
                await _bot.AnswerInlineQueryAsync(q.Id, new InlineQueryResultBase[]
                    {
                    new InlineQueryResultArticle(id: q.Id, "Нажмите, чтобы отправить реферальную ссылку", itmc)
                    },
                    cancellationToken: ct);
            }
            catch
            {
                // ignore
            }
        }
    }
}
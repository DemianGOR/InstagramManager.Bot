using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Extensions.Polling;

namespace InstagramManager.Social.Telegram
{
    public sealed class TelegramService : BackgroundService
    {
        private readonly IUpdateHandler _updateHandler;
        private readonly ITelegramBotClient _bot;

        public TelegramService(IUpdateHandler updateHandler,
            ITelegramBotClient bot)
        {
            _updateHandler = updateHandler;
            _bot = bot;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return _bot.ReceiveAsync(_updateHandler, stoppingToken);
        }
    }
}

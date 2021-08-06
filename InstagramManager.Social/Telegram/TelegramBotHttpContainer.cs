using System.Net.Http;

namespace InstagramManager.Social.Telegram
{
    public sealed class TelegramBotHttpContainer : ITelegramBotHttpContainer
    {
        public TelegramBotHttpContainer(HttpClient instance) => Instance = instance;

        public HttpClient Instance { get; }
    }
}

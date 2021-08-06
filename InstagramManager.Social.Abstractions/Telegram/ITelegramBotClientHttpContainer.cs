using System.Net.Http;

namespace InstagramManager.Social.Telegram
{
    public interface ITelegramBotClientHttpContainer
    {
        public HttpClient Instance { get; }
    }
}

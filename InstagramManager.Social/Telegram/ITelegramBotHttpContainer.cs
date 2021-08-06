using System.Net.Http;

namespace InstagramManager.Social.Telegram
{
    public interface ITelegramBotHttpContainer
    {
        HttpClient Instance { get; }
    }
}

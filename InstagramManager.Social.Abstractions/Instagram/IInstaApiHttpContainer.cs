using System.Net.Http;

namespace InstagramManager.Social.Instagram
{
    public interface IInstaApiHttpContainer
    {
        HttpClient Instance { get; }
    }
}

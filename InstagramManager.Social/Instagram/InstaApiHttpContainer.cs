using System.Net.Http;

namespace InstagramManager.Social.Instagram
{
    public sealed class InstaApiHttpContainer : IInstaApiHttpContainer
    {
        public InstaApiHttpContainer(HttpClient instance) => Instance = instance;

        public HttpClient Instance { get; }
    }
}

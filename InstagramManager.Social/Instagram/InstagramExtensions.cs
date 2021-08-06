using InstagramApiSharp.Classes;
using System.Threading.Tasks;

namespace InstagramManager.Social.Instagram
{
    internal static class InstagramExtensions
    {
        internal static async ValueTask<bool> TryHandle<T>(this IResult<T> result)
        {
            if (result.Succeeded)
                return true;

            switch (result.Info)
            {
                case { ResponseType: ResponseType.AlreadyLiked }:
                    return true;

                case { ResponseType: ResponseType.ChallengeRequired, Challenge: { } ch }:
                    {
                        return false;
                    }

                default: return false;
            }
        }
    }
}

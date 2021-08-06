using InstagramApiSharp.API;
using InstagramApiSharp.Classes.Models;
using System.Threading;
using System.Threading.Tasks;

namespace InstagramManager.Social.Instagram.State
{
    public interface IInstagramStateManager
    {
        public ValueTask<IInstaApi> GetInstaApiForUserAsync(int userId, CancellationToken ct);

        public Task<InstaUser> PushUsernameAsync(int userId, string username, CancellationToken ct);

        public Task<IInstaApi> PushPasswordAsync(int userId, string password, CancellationToken ct);

        public IInstaApi GetAdminApi();
    }
}

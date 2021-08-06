using InstagramManager.Data.Models;
using System.Threading;
using System.Threading.Tasks;

namespace InstagramManager.Social.Instagram.State
{
    public interface IInstagramAdminStateManager
    {
        public Task<bool> ProcessAdminAsync(Person adminAccount, CancellationToken ct);
    }
}

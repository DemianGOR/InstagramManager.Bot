using InstagramManager.Data.Enums;
using InstagramManager.Data.Models;
using System.Threading.Tasks;

namespace InstagramManager.Social.Instagram
{
    public interface IInstagramService
    {
        Task<TaskPassingResult> CheckIfTaskPassedAsync(int userId, string taskId);
        Task RegisterTaskAsync(InstagramTask task);
    }
}

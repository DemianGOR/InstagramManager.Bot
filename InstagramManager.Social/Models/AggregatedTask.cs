using InstagramManager.Data.Models;
using System.Collections.Generic;

namespace InstagramManager.Social.Models
{
    public sealed class AggregatedTask : InstagramTask
    {
        public List<PassedTask> PassedTasks { get; set; }

        public int PassedTasksCount { get; set; }
    }
}

using System;

namespace InstagramManager.Data.Enums
{
    [Flags]
    public enum InstagramTaskMode
    {
        LikeAndComment = 1,
        WatchingStory = 2,
        Subscribing = 4,
        SavingPost = 8
    }
}

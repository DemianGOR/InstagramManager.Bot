using InstagramApiSharp;
using InstagramApiSharp.API;
using InstagramApiSharp.Classes;
using InstagramApiSharp.Classes.Models;
using InstagramManager.Data.Context;
using InstagramManager.Data.Enums;
using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace InstagramManager.Social.Instagram.Extensions
{
    public static class InstaApiExtensions
    {
        public static async IAsyncEnumerable<TaskPassingResult> CheckIfTaskPassedAsync(this IInstaApi client, DataContext context, int userId, ObjectId taskId,
            [EnumeratorCancellation] CancellationToken ct)
        {
            var task = await context.Tasks.Find(t => t.Id == taskId)
                .FirstOrDefaultAsync(ct);
            if (task == default)
            {
                yield return TaskPassingResult.TaskNotFoundResult;
                yield break;
            }

            var mediaResult = await client.MediaProcessor.GetMediaByIdAsync(task.MediaId);
            if (!mediaResult.Succeeded)
            {
                yield return TaskPassingResult.MediaNotFoundResult;
                yield break;
            }

            var passerUsername = await context.Users.Find(u => u.Id == userId).Project(u => u.Username).FirstOrDefaultAsync(ct);
            if (passerUsername == default)
            {
                yield return TaskPassingResult.InternalErrorResult;
                yield break;
            }

            if (mediaResult.Value.Likers.Any(l => l.UserName == passerUsername))
                yield return TaskPassingResult.AsPassed("Лайк");
            else yield return TaskPassingResult.AsNotPassed("Лайк");

            var comment = mediaResult.Value.PreviewComments.FirstOrDefault(c => c.User.UserName == passerUsername);
            if (comment == null)
            {
                var paginationParams = PaginationParameters.MaxPagesToLoad(1);
                do
                {
                    var commentsResult = await client.CommentProcessor
                        .GetMediaCommentsAsync(task.MediaId, paginationParams);
                    if (!commentsResult.Succeeded)
                    {
                        yield return TaskPassingResult.AsNotPassed("Комментарий");
                        break;
                    }

                    comment = commentsResult.Value.Comments.FirstOrDefault(c => c.User.UserName == passerUsername);
                    paginationParams.NextMaxId = commentsResult.Value.NextMaxId;
                }
                while (comment == null && paginationParams.NextMaxId != null);
            }

            string[] splitComments;
            if (comment != null &&
                (splitComments = comment.Text.Split(" ,.:?\"'!();1234567890".ToCharArray(), StringSplitOptions.RemoveEmptyEntries)).Length >= 4 &&
                splitComments.Count(c => c.Length >= 3) >= 4)
                yield return TaskPassingResult.AsPassed("Комментарий");
            else yield return TaskPassingResult.AsNotPassed("Комментарий");

            if (task.Mode.HasFlag(InstagramTaskMode.WatchingStory))
            {
                IResult<InstaUserInfo> userResult;
                IResult<InstaStory> storyResult;

                if (!(userResult = await client.UserProcessor.GetUserInfoByUsernameAsync(mediaResult.Value.User.UserName)).Succeeded ||
                    !(storyResult = await client.StoryProcessor.GetUserStoryAsync(userResult.Value.Pk)).Succeeded)
                    yield return TaskPassingResult.AsError("Просмотр историй");
                else if (storyResult.Value.Items.All(it => it.Viewers.Find(v => v.UserName == passerUsername) != default))
                    yield return TaskPassingResult.AsPassed("Просмотр историй");
                else
                    yield return TaskPassingResult.AsNotPassed("Просмотр историй");
            }

            if (task.Mode.HasFlag(InstagramTaskMode.SavingPost))
            {
                // TODO
                yield return TaskPassingResult.AsPassed("Сохранение поста");
            }

            if (task.Mode.HasFlag(InstagramTaskMode.Subscribing))
            {
                var userFollowersResult = await client.UserProcessor
                    .GetUserFollowersAsync(mediaResult.Value.User.UserName, PaginationParameters.MaxPagesToLoad(1), passerUsername);

                if (!userFollowersResult.Succeeded)
                    yield return TaskPassingResult.AsError("Подписка");
                else if (userFollowersResult.Value.Any(follower => follower.UserName == passerUsername))
                    yield return TaskPassingResult.AsPassed("Подписка");
                else
                    yield return TaskPassingResult.AsNotPassed("Подписка");
            }
        }
    }
}

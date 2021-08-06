namespace InstagramManager.Data.Enums
{
    public sealed class TaskPassingResult
    {
        private TaskPassingResult() { }

        public bool Succeeded { get; private set; }
        public string Name { get; private set; }
        public bool IsPassed { get; private set; }
        public bool MediaNotFound { get; private set; }
        public bool InternalError { get; private set; }
        public bool TaskNotFound { get; private set; }

        public static TaskPassingResult TaskNotFoundResult =>
            new TaskPassingResult { TaskNotFound = true };

        public static TaskPassingResult MediaNotFoundResult =>
            new TaskPassingResult { MediaNotFound = true };

        public static TaskPassingResult InternalErrorResult =>
            new TaskPassingResult { InternalError = true };

        public static TaskPassingResult AsPassed(string name) =>
            new TaskPassingResult { Name = name, IsPassed = true, Succeeded = true };

        public static TaskPassingResult AsNotPassed(string name) =>
            new TaskPassingResult { Name = name, Succeeded = true };

        public static TaskPassingResult AsError(string name) =>
            new TaskPassingResult { Name = name };
    }
}

namespace TaskManager.Contracts;

public static class TaskStatuses
{
    public const string NewTask = "new_task";
    public const string InDevelopment = "in_development";
    public const string InQa = "in_qa";
    public const string InCodeReview = "in_code_review";
    public const string ReadyForRelease = "ready_for_release";
    public const string Released = "released";
    public const string Archived = "archived";

    public static readonly IReadOnlyList<string> All = [NewTask, InDevelopment, InQa, InCodeReview, ReadyForRelease, Released, Archived];

    public static bool IsOverdue(DateOnly? dateToFinish, string status, DateOnly today) =>
        dateToFinish is { } due && due < today && status is not (Released or Archived);
}

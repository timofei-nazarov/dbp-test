using TaskManager.Contracts;

namespace TaskManager.Web;

public static class BoardStatus
{
    public static string Label(string status) => status switch
    {
        TaskStatuses.NewTask => "New",
        TaskStatuses.InDevelopment => "In development",
        TaskStatuses.InQa => "In QA",
        TaskStatuses.InCodeReview => "In code review",
        TaskStatuses.ReadyForRelease => "Ready for release",
        TaskStatuses.Released => "Released",
        TaskStatuses.Archived => "Archived",
        _ => status
    };

    public static string Token(string status) => status switch
    {
        TaskStatuses.NewTask => "new",
        TaskStatuses.InDevelopment => "in-development",
        TaskStatuses.InQa => "in-qa",
        TaskStatuses.InCodeReview => "in-code-review",
        TaskStatuses.ReadyForRelease => "ready-for-release",
        TaskStatuses.Released => "released",
        TaskStatuses.Archived => "archived",
        _ => "new"
    };
}

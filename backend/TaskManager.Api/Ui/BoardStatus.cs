namespace TaskManager.Api.Ui;

public static class BoardStatus
{
    public static readonly IReadOnlyList<string> Values =
    [
        "new_task",
        "in_development",
        "in_qa",
        "in_code_review",
        "ready_for_release",
        "released",
        "archived"
    ];

    public static string Label(string status) => status switch
    {
        "new_task" => "New",
        "in_development" => "In development",
        "in_qa" => "In QA",
        "in_code_review" => "In code review",
        "ready_for_release" => "Ready for release",
        "released" => "Released",
        "archived" => "Archived",
        _ => status
    };
}

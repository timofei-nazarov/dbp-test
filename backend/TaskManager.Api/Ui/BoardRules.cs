using System.Globalization;
using TaskManager.Api.Api;

namespace TaskManager.Api.Ui;

public static class BoardRules
{
    public static bool IsOverdue(DateOnly? due, string condition, DateOnly today) =>
        due is not null && due < today && condition is not ("released" or "archived");

    public static IReadOnlyList<(string Status, IReadOnlyList<TaskResponse> Tasks)> Group(IEnumerable<TaskResponse> tasks)
    {
        var ordered = tasks.OrderBy(task => task.Id).ToList();
        return BoardStatus.Values
            .Select(status => (status, (IReadOnlyList<TaskResponse>)ordered.Where(task => task.Condition == status).ToList()))
            .ToList();
    }

    public static string? ExecutorName(UserResponse? executor)
    {
        if (executor is null)
        {
            return null;
        }
        var fullName = $"{executor.FirstName} {executor.LastName}".Trim();
        return fullName.Length > 0 ? fullName : executor.Username;
    }

    public static string? DueText(DateOnly? due) => due?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}

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

    public static string Initials(string? firstName, string? lastName, string? username)
    {
        var names = new[] { firstName, lastName }.Select(FirstElement).Where(element => element.Length > 0).ToList();
        if (names.Count > 0)
        {
            return string.Concat(names).ToUpperInvariant();
        }
        var fallback = FirstElement(username);
        return fallback.Length > 0 ? fallback.ToUpperInvariant() : "?";
    }

    public static int CountOverdue(IEnumerable<TaskResponse> tasks, DateOnly today) =>
        tasks.Count(task => IsOverdue(task.DateToFinish, task.Condition, today));

    public static string Summary(IReadOnlyCollection<TaskResponse>? tasks, DateOnly today, bool isStaff)
    {
        var access = isStaff ? "You can change status" : "View only";
        if (tasks is null)
        {
            return access;
        }
        var total = tasks.Count;
        return $"{total} {(total == 1 ? "task" : "tasks")} · {CountOverdue(tasks, today)} overdue · {access}";
    }

    private static string FirstElement(string? text)
    {
        var trimmed = text?.Trim();
        return string.IsNullOrEmpty(trimmed) ? "" : StringInfo.GetNextTextElement(trimmed);
    }
}

using System.Globalization;
using TaskManager.Contracts;

namespace TaskManager.Web;

public static class BoardRules
{
    public static IReadOnlyList<(string Status, IReadOnlyList<TaskResponse> Tasks)> Group(IEnumerable<TaskResponse> tasks)
    {
        var ordered = tasks.OrderBy(task => task.Id).ToList();
        return TaskStatuses.All
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

    public static string? DueText(DateOnly? due, DateOnly today)
    {
        if (due is not { } value)
        {
            return null;
        }
        var format = value.Year == today.Year ? "MMM d" : "MMM d, yyyy";
        return value.ToString(format, CultureInfo.InvariantCulture);
    }

    public static string OverdueLabel(int days) => $"Overdue · {days}d";

    public static string OverdueAccessibleText(int days) => $"Overdue by {days} {(days == 1 ? "day" : "days")}";

    public static string CountText(int count) => $"{count} {(count == 1 ? "task" : "tasks")}";

    public static string ShownText(int count) => count == 0 ? "No tasks shown" : $"{CountText(count)} shown";

    public static int AvatarTone(long userId) => (int)(((userId % 8) + 8) % 8);

    public static IReadOnlyList<ExecutorOption> ExecutorOptions(IEnumerable<TaskResponse> tasks)
    {
        var executors = tasks
            .Where(task => task.Executor is not null)
            .Select(task => task.Executor!)
            .GroupBy(executor => executor.Id)
            .Select(group => group.First())
            .Select(executor => (executor.Id, Label: ExecutorName(executor)!))
            .OrderBy(item => item.Label, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Id)
            .Select(item => new ExecutorOption(item.Id.ToString(CultureInfo.InvariantCulture), item.Label));
        return [new ExecutorOption(BoardFilter.AllExecutors, "All executors"), new ExecutorOption(BoardFilter.Unassigned, "Unassigned"), .. executors];
    }

    public static IReadOnlyList<TaskResponse> Filter(IEnumerable<TaskResponse> tasks, BoardFilter filter, bool statusApplies) =>
        tasks
            .Where(task => MatchesSearchAndExecutor(task, filter))
            .Where(task => !filter.OverdueOnly || task.OverdueDays is not null)
            .Where(task => !statusApplies || filter.Status is null || task.Condition == filter.Status)
            .ToList();

    public static int CountMatching(IEnumerable<TaskResponse> tasks, BoardFilter filter) =>
        tasks.Count(task => MatchesSearchAndExecutor(task, filter));

    public static int CountOverdueMatching(IEnumerable<TaskResponse> tasks, BoardFilter filter) =>
        tasks.Count(task => MatchesSearchAndExecutor(task, filter) && task.OverdueDays is not null);

    public static int CountStatus(IEnumerable<TaskResponse> tasks, BoardFilter filter, string status) =>
        tasks.Count(task => task.Condition == status
            && MatchesSearchAndExecutor(task, filter)
            && (!filter.OverdueOnly || task.OverdueDays is not null));

    private static bool MatchesSearchAndExecutor(TaskResponse task, BoardFilter filter)
    {
        var search = filter.Search.Trim();
        if (search.Length > 0 && !task.Title.Contains(search, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        return filter.Executor switch
        {
            BoardFilter.AllExecutors => true,
            BoardFilter.Unassigned => task.Executor is null,
            var id => task.Executor is not null && task.Executor.Id.ToString(CultureInfo.InvariantCulture) == id
        };
    }

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

    public static int CountOverdue(IEnumerable<TaskResponse> tasks) =>
        tasks.Count(task => task.OverdueDays is not null);

    public static string Summary(IReadOnlyCollection<TaskResponse>? tasks, bool isStaff)
    {
        var access = isStaff ? "You can change status" : "View only";
        if (tasks is null)
        {
            return access;
        }
        var total = tasks.Count;
        return $"{CountText(total)} · {CountOverdue(tasks)} overdue · {access}";
    }

    private static string FirstElement(string? text)
    {
        var trimmed = text?.Trim();
        return string.IsNullOrEmpty(trimmed) ? "" : StringInfo.GetNextTextElement(trimmed);
    }
}

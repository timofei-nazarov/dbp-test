using TaskManager.Contracts;

namespace TaskManager.Domain;

public static class TaskRules
{
    public static bool IsOverdue(DateOnly? dateToFinish, string status, DateOnly today) =>
        dateToFinish is { } due && due < today && status is not (TaskStatuses.Released or TaskStatuses.Archived);

    public static int? OverdueDays(DateOnly? dateToFinish, string status, DateOnly today) =>
        IsOverdue(dateToFinish, status, today) ? today.DayNumber - dateToFinish!.Value.DayNumber : null;
}

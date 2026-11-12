using TaskManager.Contracts;
using TaskManager.Web;
using Xunit;

namespace TaskManager.Web.Tests;

public sealed class BoardRulesTests
{
    private static readonly DateOnly Today = new(2026, 11, 10);

    private static TaskResponse Task(long id, string condition, UserResponse? executor = null, DateOnly? due = null, bool overdue = false) =>
        new(id, $"Task {id}", null, Today, Today, due, condition, null, null, executor, [], overdue);

    private static UserResponse User(string username, string firstName = "", string lastName = "") =>
        new(1, username, firstName, lastName, "", "developer", null);

    [Fact]
    public void GroupReturnsSevenColumnsInBoardOrderWithEmptyColumnsKept()
    {
        var columns = BoardRules.Group([Task(2, "in_qa"), Task(1, "in_qa")]);

        Assert.Equal(TaskStatuses.All, columns.Select(column => column.Status));
        Assert.Equal(7, columns.Count);
        Assert.Equal([1L, 2L], columns.Single(column => column.Status == "in_qa").Tasks.Select(task => task.Id));
        Assert.Equal(6, columns.Count(column => column.Status != "in_qa" && column.Tasks.Count == 0));
    }

    [Fact]
    public void GroupPlacesEveryTaskInExactlyOneColumn()
    {
        var tasks = TaskStatuses.All.Select((status, index) => Task(index + 1, status)).Append(Task(8, "new_task")).ToList();

        var columns = BoardRules.Group(tasks);

        Assert.Equal(tasks.Count, columns.Sum(column => column.Tasks.Count));
        Assert.Equal(tasks.Select(task => task.Id).Order(), columns.SelectMany(column => column.Tasks).Select(task => task.Id).Order());
        Assert.All(columns, column => Assert.All(column.Tasks, task => Assert.Equal(column.Status, task.Condition)));
    }

    [Fact]
    public void ExecutorNameUsesFullNameThenUsernameThenNull()
    {
        Assert.Equal("Ada Lovelace", BoardRules.ExecutorName(User("ada", "Ada", "Lovelace")));
        Assert.Equal("Ada", BoardRules.ExecutorName(User("ada", "Ada", "")));
        Assert.Equal("ada", BoardRules.ExecutorName(User("ada")));
        Assert.Null(BoardRules.ExecutorName(null));
    }

    [Fact]
    public void DueTextUsesInvariantIsoFormat()
    {
        Assert.Equal("2026-11-10", BoardRules.DueText(new DateOnly(2026, 11, 10)));
        Assert.Equal("2026-01-05", BoardRules.DueText(new DateOnly(2026, 1, 5)));
        Assert.Null(BoardRules.DueText(null));
    }

    [Fact]
    public void InitialsUseFirstLettersOfFirstAndLastName()
    {
        Assert.Equal("AS", BoardRules.Initials("Anna", "Smith", "anna"));
        Assert.Equal("AS", BoardRules.Initials("anna", "smith", "x"));
    }

    [Fact]
    public void InitialsUseSingleNameWhenOnlyOneExists()
    {
        Assert.Equal("A", BoardRules.Initials("Anna", "", "anna"));
        Assert.Equal("S", BoardRules.Initials("", "Smith", "anna"));
        Assert.Equal("S", BoardRules.Initials("  ", "Smith", "anna"));
    }

    [Fact]
    public void InitialsFallBackToUsernameWhenBothNamesEmpty()
    {
        Assert.Equal("A", BoardRules.Initials("", "", "anna"));
        Assert.Equal("A", BoardRules.Initials(null, null, "anna"));
        Assert.Equal("?", BoardRules.Initials(" ", null, ""));
    }

    private static List<TaskResponse> SixTasksWithFourOverdue() =>
    [
        Task(1, "new_task", due: Today.AddDays(-1), overdue: true),
        Task(2, "in_qa", due: Today.AddDays(-5), overdue: true),
        Task(3, "ready_for_release", due: Today.AddDays(-2), overdue: true),
        Task(4, "in_development", due: Today.AddDays(-9), overdue: true),
        Task(5, "released", due: Today.AddDays(-9)),
        Task(6, "in_qa", due: Today.AddDays(1))
    ];

    [Fact]
    public void SummaryTextForStaffAndReader()
    {
        var tasks = SixTasksWithFourOverdue();

        Assert.Equal("6 tasks · 4 overdue · You can change status", BoardRules.Summary(tasks, true));
        Assert.Equal("6 tasks · 4 overdue · View only", BoardRules.Summary(tasks, false));
    }

    [Fact]
    public void SummaryTextUsesSingularAndZero()
    {
        Assert.Equal("1 task · 0 overdue · View only", BoardRules.Summary([Task(1, "new_task")], false));
        Assert.Equal("0 tasks · 0 overdue · You can change status", BoardRules.Summary([], true));
    }

    [Fact]
    public void SummaryTextWithoutTasksShowsOnlyAccess()
    {
        Assert.Equal("You can change status", BoardRules.Summary(null, true));
        Assert.Equal("View only", BoardRules.Summary(null, false));
    }

    [Fact]
    public void CountOverdueCountsOnlyTasksFlaggedByApi()
    {
        List<TaskResponse> tasks =
        [
            Task(1, "new_task", due: Today.AddDays(-1), overdue: true),
            Task(2, "in_qa", due: Today.AddDays(-3), overdue: true),
            Task(3, "in_qa", due: Today.AddDays(-1), overdue: false),
            Task(4, "in_qa")
        ];

        Assert.Equal(2, BoardRules.CountOverdue(tasks));
    }
}

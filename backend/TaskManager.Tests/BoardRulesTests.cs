using TaskManager.Api.Api;
using TaskManager.Api.Ui;
using Xunit;

namespace TaskManager.Tests;

public sealed class BoardRulesTests
{
    private static readonly DateOnly Today = new(2026, 11, 10);

    private static TaskResponse Task(long id, string condition, UserResponse? executor = null, DateOnly? due = null) =>
        new(id, $"Task {id}", null, Today, Today, due, condition, null, null, executor, [], BoardRules.IsOverdue(due, condition, Today));

    private static UserResponse User(string username, string firstName = "", string lastName = "") =>
        new(1, username, firstName, lastName, "", "developer", null);

    [Theory]
    [InlineData("new_task")]
    [InlineData("in_development")]
    [InlineData("in_qa")]
    [InlineData("in_code_review")]
    [InlineData("ready_for_release")]
    public void OverdueRequiresPastDueDateAndActiveStatus(string condition)
    {
        Assert.True(BoardRules.IsOverdue(Today.AddDays(-1), condition, Today));
    }

    [Fact]
    public void DueTodayAndFutureDatesAreNotOverdue()
    {
        Assert.False(BoardRules.IsOverdue(Today, "in_qa", Today));
        Assert.False(BoardRules.IsOverdue(Today.AddDays(3), "in_qa", Today));
    }

    [Theory]
    [InlineData("released")]
    [InlineData("archived")]
    public void ReleasedAndArchivedAreNeverOverdue(string condition)
    {
        Assert.False(BoardRules.IsOverdue(Today.AddDays(-30), condition, Today));
    }

    [Fact]
    public void MissingDueDateIsNeverOverdue()
    {
        Assert.False(BoardRules.IsOverdue(null, "new_task", Today));
    }

    [Fact]
    public void GroupReturnsSevenColumnsInBoardOrderWithEmptyColumnsKept()
    {
        var columns = BoardRules.Group([Task(2, "in_qa"), Task(1, "in_qa")]);

        Assert.Equal(BoardStatus.Values, columns.Select(column => column.Status));
        Assert.Equal(7, columns.Count);
        Assert.Equal([1L, 2L], columns.Single(column => column.Status == "in_qa").Tasks.Select(task => task.Id));
        Assert.Equal(6, columns.Count(column => column.Status != "in_qa" && column.Tasks.Count == 0));
    }

    [Fact]
    public void GroupPlacesEveryTaskInExactlyOneColumn()
    {
        var tasks = BoardStatus.Values.Select((status, index) => Task(index + 1, status)).Append(Task(8, "new_task")).ToList();

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
        Task(1, "new_task", due: Today.AddDays(-1)),
        Task(2, "in_qa", due: Today.AddDays(-5)),
        Task(3, "ready_for_release", due: Today.AddDays(-2)),
        Task(4, "in_development", due: Today.AddDays(-9)),
        Task(5, "released", due: Today.AddDays(-9)),
        Task(6, "in_qa", due: Today.AddDays(1))
    ];

    [Fact]
    public void SummaryTextForStaffAndReader()
    {
        var tasks = SixTasksWithFourOverdue();

        Assert.Equal("6 tasks · 4 overdue · You can change status", BoardRules.Summary(tasks, Today, true));
        Assert.Equal("6 tasks · 4 overdue · View only", BoardRules.Summary(tasks, Today, false));
    }

    [Fact]
    public void SummaryTextUsesSingularAndZero()
    {
        Assert.Equal("1 task · 0 overdue · View only", BoardRules.Summary([Task(1, "new_task")], Today, false));
        Assert.Equal("0 tasks · 0 overdue · You can change status", BoardRules.Summary([], Today, true));
    }

    [Fact]
    public void SummaryTextWithoutTasksShowsOnlyAccess()
    {
        Assert.Equal("You can change status", BoardRules.Summary(null, Today, true));
        Assert.Equal("View only", BoardRules.Summary(null, Today, false));
    }

    [Fact]
    public void CountOverdueIgnoresReleasedArchivedAndMissingDates()
    {
        List<TaskResponse> tasks =
        [
            Task(1, "new_task", due: Today.AddDays(-1)),
            Task(2, "released", due: Today.AddDays(-1)),
            Task(3, "archived", due: Today.AddDays(-1)),
            Task(4, "in_qa"),
            Task(5, "in_qa", due: Today)
        ];

        Assert.Equal(1, BoardRules.CountOverdue(tasks, Today));
    }
}

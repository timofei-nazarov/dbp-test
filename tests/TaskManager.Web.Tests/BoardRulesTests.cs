using TaskManager.Contracts;
using TaskManager.Web;
using Xunit;

namespace TaskManager.Web.Tests;

public sealed class BoardRulesTests
{
    private static readonly DateOnly Today = new(2026, 11, 10);

    private static TaskResponse Task(long id, string condition, UserResponse? executor = null, DateOnly? due = null, bool overdue = false, string? title = null) =>
        new(id, title ?? $"Task {id}", null, Today, Today, due, condition, null, null, executor, [], overdue, overdue && due is not null ? Today.DayNumber - due.Value.DayNumber : null);

    private static UserResponse User(string username, string firstName = "", string lastName = "", long id = 1) =>
        new(id, username, firstName, lastName, "", "developer", null);

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
    public void DueTextUsesEnglishMonthAndDay()
    {
        Assert.Equal("Nov 4", BoardRules.DueText(new DateOnly(2026, 11, 4), Today));
        Assert.Equal("Jan 5", BoardRules.DueText(new DateOnly(2026, 1, 5), Today));
        Assert.Null(BoardRules.DueText(null, Today));
    }

    [Fact]
    public void DueTextAddsYearForAnotherUtcYear()
    {
        Assert.Equal("Nov 4, 2027", BoardRules.DueText(new DateOnly(2027, 11, 4), Today));
        Assert.Equal("Dec 31, 2025", BoardRules.DueText(new DateOnly(2025, 12, 31), Today));
    }

    [Fact]
    public void OverdueLabelAndAccessibleText()
    {
        Assert.Equal("Overdue · 6d", BoardRules.OverdueLabel(6));
        Assert.Equal("Overdue by 6 days", BoardRules.OverdueAccessibleText(6));
        Assert.Equal("Overdue · 1d", BoardRules.OverdueLabel(1));
        Assert.Equal("Overdue by 1 day", BoardRules.OverdueAccessibleText(1));
    }

    [Fact]
    public void CountTextAndShownText()
    {
        Assert.Equal("1 task", BoardRules.CountText(1));
        Assert.Equal("2 tasks", BoardRules.CountText(2));
        Assert.Equal("0 tasks", BoardRules.CountText(0));
        Assert.Equal("1 task shown", BoardRules.ShownText(1));
        Assert.Equal("5 tasks shown", BoardRules.ShownText(5));
        Assert.Equal("No tasks shown", BoardRules.ShownText(0));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(7, 7)]
    [InlineData(8, 0)]
    [InlineData(15, 7)]
    public void AvatarToneIsUserIdModEight(long id, int tone)
    {
        Assert.Equal(tone, BoardRules.AvatarTone(id));
    }

    [Fact]
    public void ExecutorOptionsAreAllUnassignedThenSortedByLabelThenId()
    {
        var zed = User("zed", "Zed", "Last", 4);
        var bob = User("bob", id: 9);
        var bobUpper = User("BOB2", "Bob", "", 3);
        var ann = User("ann", "Ann", "Lee", 2);
        List<TaskResponse> tasks =
        [
            Task(1, "new_task", zed),
            Task(2, "new_task", zed),
            Task(3, "new_task", bob),
            Task(4, "in_qa", bobUpper),
            Task(5, "in_qa", ann),
            Task(6, "in_qa")
        ];

        var options = BoardRules.ExecutorOptions(tasks);

        Assert.Equal(["", "unassigned", "2", "3", "9", "4"], options.Select(option => option.Value));
        Assert.Equal(["All executors", "Unassigned", "Ann Lee", "Bob", "bob", "Zed Last"], options.Select(option => option.Label));
    }

    [Fact]
    public void ExecutorOptionsWithoutExecutorsHaveOnlyTwoEntries()
    {
        Assert.Equal(["All executors", "Unassigned"], BoardRules.ExecutorOptions([Task(1, "new_task")]).Select(option => option.Label));
    }

    [Fact]
    public void FilterSearchIsTrimmedCaseInsensitiveSubstring()
    {
        List<TaskResponse> tasks = [Task(1, "new_task", title: "Fix Login page"), Task(2, "new_task", title: "Write docs")];

        Assert.Equal([1L], BoardRules.Filter(tasks, new BoardFilter(Search: "  LOGIN "), false).Select(task => task.Id));
        Assert.Equal([1L, 2L], BoardRules.Filter(tasks, new BoardFilter(Search: "   "), false).Select(task => task.Id));
        Assert.Empty(BoardRules.Filter(tasks, new BoardFilter(Search: "zzz"), false));
    }

    [Fact]
    public void FilterCombinesSearchExecutorOverdueWithAnd()
    {
        var ann = User("ann", "Ann", "Lee", 2);
        var bob = User("bob", id: 3);
        List<TaskResponse> tasks =
        [
            Task(1, "new_task", ann, Today.AddDays(-2), true, "Alpha one"),
            Task(2, "new_task", ann, null, false, "Alpha two"),
            Task(3, "new_task", bob, Today.AddDays(-2), true, "Alpha three"),
            Task(4, "new_task", ann, Today.AddDays(-2), true, "Beta")
        ];

        var filter = new BoardFilter("alpha", "2", true);

        Assert.Equal([1L], BoardRules.Filter(tasks, filter, false).Select(task => task.Id));
    }

    [Fact]
    public void FilterStatusAppliesOnlyWhenStatusAppliesIsTrue()
    {
        List<TaskResponse> tasks = [Task(1, "new_task"), Task(2, "in_qa")];
        var filter = new BoardFilter(Status: "in_qa");

        Assert.Equal([2L], BoardRules.Filter(tasks, filter, true).Select(task => task.Id));
        Assert.Equal([1L, 2L], BoardRules.Filter(tasks, filter, false).Select(task => task.Id));
    }

    [Fact]
    public void FilterUnassignedMatchesTasksWithoutExecutor()
    {
        List<TaskResponse> tasks = [Task(1, "new_task"), Task(2, "new_task", User("ann", id: 2))];

        Assert.Equal([1L], BoardRules.Filter(tasks, new BoardFilter(Executor: BoardFilter.Unassigned), false).Select(task => task.Id));
        Assert.Equal([2L], BoardRules.Filter(tasks, new BoardFilter(Executor: "2"), false).Select(task => task.Id));
    }

    [Fact]
    public void CountsFollowCardRules()
    {
        var ann = User("ann", id: 2);
        List<TaskResponse> tasks =
        [
            Task(1, "new_task", ann, Today.AddDays(-1), true, "Match a"),
            Task(2, "new_task", ann, null, false, "Match b"),
            Task(3, "in_qa", ann, Today.AddDays(-4), true, "Match c"),
            Task(4, "in_qa", null, Today.AddDays(-4), true, "Match d"),
            Task(5, "in_qa", ann, null, false, "Other")
        ];
        var filter = new BoardFilter("match", "2", true, "in_qa");

        Assert.Equal(3, BoardRules.CountMatching(tasks, filter));
        Assert.Equal(2, BoardRules.CountOverdueMatching(tasks, filter));
        Assert.Equal(1, BoardRules.CountStatus(tasks, filter, "new_task"));
        Assert.Equal(1, BoardRules.CountStatus(tasks, filter, "in_qa"));
        Assert.Equal(0, BoardRules.CountStatus(tasks, filter, "released"));
        Assert.Equal(2, BoardRules.CountStatus(tasks, filter with { OverdueOnly = false }, "new_task"));
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
    public void CountOverdueUsesOverdueDays()
    {
        List<TaskResponse> tasks =
        [
            Task(1, "new_task", due: Today.AddDays(-1), overdue: true),
            Task(2, "in_qa", due: Today.AddDays(-3), overdue: true),
            Task(3, "in_qa", due: Today.AddDays(-1), overdue: false) with { IsOverdue = true },
            Task(4, "in_qa")
        ];

        Assert.Equal(2, BoardRules.CountOverdue(tasks));
    }

    [Fact]
    public void SummaryStillCountsAllLoadedTasks()
    {
        var tasks = SixTasksWithFourOverdue();

        Assert.Equal(6, tasks.Count);
        Assert.Equal("6 tasks · 4 overdue · View only", BoardRules.Summary(tasks, false));
    }

    [Theory]
    [InlineData("new_task", "new")]
    [InlineData("in_development", "in-development")]
    [InlineData("in_qa", "in-qa")]
    [InlineData("in_code_review", "in-code-review")]
    [InlineData("ready_for_release", "ready-for-release")]
    [InlineData("released", "released")]
    [InlineData("archived", "archived")]
    public void StatusTokenIsCssSuffix(string status, string token)
    {
        Assert.Equal(token, BoardStatus.Token(status));
    }
}

using TaskManager.Api.Api;
using TaskManager.Api.Ui;
using Xunit;

namespace TaskManager.Tests;

public sealed class BoardRulesTests
{
    private static readonly DateOnly Today = new(2026, 11, 10);

    private static TaskResponse Task(long id, string condition, UserResponse? executor = null, DateOnly? due = null) =>
        new(id, $"Task {id}", null, Today, Today, due, condition, null, null, executor, []);

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
}

using TaskManager.Contracts;
using TaskManager.Domain;
using Xunit;

namespace TaskManager.Api.Tests;

public sealed class TaskRulesTests
{
    private static readonly DateOnly Today = new(2026, 11, 10);

    [Theory]
    [InlineData("new_task")]
    [InlineData("in_development")]
    [InlineData("in_qa")]
    [InlineData("in_code_review")]
    [InlineData("ready_for_release")]
    public void OverdueRequiresPastDueDateAndActiveStatus(string status)
    {
        Assert.True(TaskRules.IsOverdue(Today.AddDays(-1), status, Today));
    }

    [Fact]
    public void DueTodayAndFutureDatesAreNotOverdue()
    {
        Assert.False(TaskRules.IsOverdue(Today, TaskStatuses.InQa, Today));
        Assert.False(TaskRules.IsOverdue(Today.AddDays(3), TaskStatuses.InQa, Today));
    }

    [Theory]
    [InlineData("released")]
    [InlineData("archived")]
    public void ReleasedAndArchivedAreNeverOverdue(string status)
    {
        Assert.False(TaskRules.IsOverdue(Today.AddDays(-30), status, Today));
    }

    [Fact]
    public void MissingDueDateIsNeverOverdue()
    {
        Assert.False(TaskRules.IsOverdue(null, TaskStatuses.NewTask, Today));
    }

    [Fact]
    public void OverdueDaysIsWholeDaysSinceDueDateWhenOverdue()
    {
        Assert.Equal(6, TaskRules.OverdueDays(new DateOnly(2026, 11, 4), TaskStatuses.InQa, Today));
        Assert.Equal(1, TaskRules.OverdueDays(Today.AddDays(-1), TaskStatuses.NewTask, Today));
    }

    [Theory]
    [InlineData(0, "in_qa")]
    [InlineData(3, "in_qa")]
    [InlineData(null, "new_task")]
    [InlineData(-5, "released")]
    [InlineData(-5, "archived")]
    public void OverdueDaysIsNullWhenNotOverdue(int? offset, string status)
    {
        DateOnly? due = offset is { } days ? Today.AddDays(days) : null;
        Assert.Null(TaskRules.OverdueDays(due, status, Today));
    }

    [Theory]
    [InlineData(-10, "new_task")]
    [InlineData(-1, "in_development")]
    [InlineData(0, "in_qa")]
    [InlineData(4, "in_code_review")]
    [InlineData(-3, "ready_for_release")]
    [InlineData(-3, "released")]
    [InlineData(-3, "archived")]
    [InlineData(null, "new_task")]
    public void OverdueDaysIsNotNullExactlyWhenOverdue(int? offset, string status)
    {
        DateOnly? due = offset is { } days ? Today.AddDays(days) : null;
        Assert.Equal(TaskRules.IsOverdue(due, status, Today), TaskRules.OverdueDays(due, status, Today) is not null);
    }
}

public sealed class WorkTaskDefaultsTests
{
    [Fact]
    public void NewTaskDefaultsToNewTaskStatus()
    {
        Assert.Equal("new_task", new WorkTask().Condition);
    }
}

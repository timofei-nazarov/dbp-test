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
}

public sealed class WorkTaskDefaultsTests
{
    [Fact]
    public void NewTaskDefaultsToNewTaskStatus()
    {
        Assert.Equal("new_task", new WorkTask().Condition);
    }
}

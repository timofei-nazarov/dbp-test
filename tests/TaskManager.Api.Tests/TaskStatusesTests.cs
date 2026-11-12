using TaskManager.Contracts;
using Xunit;

namespace TaskManager.Api.Tests;

public sealed class TaskStatusesTests
{
    private static readonly DateOnly Today = new(2026, 11, 10);

    [Fact]
    public void AllListsSevenStatusesInBoardOrder()
    {
        Assert.Equal(["new_task", "in_development", "in_qa", "in_code_review", "ready_for_release", "released", "archived"], TaskStatuses.All);
    }

    [Theory]
    [InlineData("new_task")]
    [InlineData("in_development")]
    [InlineData("in_qa")]
    [InlineData("in_code_review")]
    [InlineData("ready_for_release")]
    public void OverdueRequiresPastDueDateAndActiveStatus(string status)
    {
        Assert.True(TaskStatuses.IsOverdue(Today.AddDays(-1), status, Today));
    }

    [Fact]
    public void DueTodayAndFutureDatesAreNotOverdue()
    {
        Assert.False(TaskStatuses.IsOverdue(Today, TaskStatuses.InQa, Today));
        Assert.False(TaskStatuses.IsOverdue(Today.AddDays(3), TaskStatuses.InQa, Today));
    }

    [Theory]
    [InlineData("released")]
    [InlineData("archived")]
    public void ReleasedAndArchivedAreNeverOverdue(string status)
    {
        Assert.False(TaskStatuses.IsOverdue(Today.AddDays(-30), status, Today));
    }

    [Fact]
    public void MissingDueDateIsNeverOverdue()
    {
        Assert.False(TaskStatuses.IsOverdue(null, TaskStatuses.NewTask, Today));
    }
}

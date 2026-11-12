using TaskManager.Contracts;
using Xunit;

namespace TaskManager.Api.Tests;

public sealed class TaskStatusesTests
{
    [Fact]
    public void AllListsSevenStatusesInBoardOrder()
    {
        Assert.Equal(["new_task", "in_development", "in_qa", "in_code_review", "ready_for_release", "released", "archived"], TaskStatuses.All);
    }
}

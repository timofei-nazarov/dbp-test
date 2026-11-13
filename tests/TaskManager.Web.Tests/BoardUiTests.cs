using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TaskManager.Contracts;
using TaskManager.Web.Components;
using Xunit;

namespace TaskManager.Web.Tests;

public sealed class BoardUiTests
{
    private static readonly DateOnly Today = new(2026, 11, 10);

    private static TaskResponse Task(long id, string title, string condition, UserResponse? executor = null, DateOnly? due = null, int? overdueDays = null) =>
        new(id, title, null, Today, Today, due, condition, null, null, executor, [], overdueDays is not null, overdueDays);

    private static async Task<string> RenderComponent<T>(Dictionary<string, object?> parameters) where T : IComponent
    {
        var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<T>(ParameterView.FromDictionary(parameters))).ToHtmlString());
        return WebUtility.HtmlDecode(Regex.Replace(html, "<!--.*?-->", "", RegexOptions.Singleline));
    }

    private static Task<string> Render(TaskResponse item, bool isStaff, bool mobile = false) =>
        RenderComponent<TaskCard>(new()
        {
            ["Item"] = item,
            ["IsStaff"] = isStaff,
            ["Mobile"] = mobile,
            ["Today"] = Today
        });

    private static (string Attributes, string Text) MoveButton(string html)
    {
        var match = Regex.Match(html, "<button(?<attributes>[^>]*class=\"move-button[^\"]*\"[^>]*)>(?<text>.*?)</button>", RegexOptions.Singleline);
        Assert.True(match.Success, "move button not found");
        return (match.Groups["attributes"].Value, match.Groups["text"].Value);
    }

    private static UserResponse User(long id, string username, string first = "", string last = "") =>
        new(id, username, first, last, "", "developer", null);

    [Theory]
    [InlineData("new_task", "New")]
    [InlineData("in_development", "In development")]
    [InlineData("in_qa", "In QA")]
    [InlineData("in_code_review", "In code review")]
    [InlineData("ready_for_release", "Ready for release")]
    [InlineData("released", "Released")]
    [InlineData("archived", "Archived")]
    public async Task StaffMoveButtonNameContainsStatusLabel(string condition, string name)
    {
        var html = await Render(Task(1, "Some task", condition), true);

        Assert.Contains($"Status: {name}", MoveButton(html).Text);
        Assert.DoesNotContain("Item.Condition", html);
    }

    [Fact]
    public async Task MoveButtonIsDescribedByTaskTitle()
    {
        var html = await Render(Task(42, "Fix login page", "in_qa"), true);
        var (attributes, text) = MoveButton(html);

        Assert.DoesNotContain("aria-label=", attributes);
        Assert.DoesNotContain("aria-labelledby=", attributes);
        Assert.Contains("In QA", text);
        Assert.Contains("aria-describedby=\"task-title-42\"", attributes);
        var title = Regex.Match(html, "id=\"task-title-42\"[^>]*>(?<text>.*?)</", RegexOptions.Singleline);
        Assert.True(title.Success);
        Assert.Equal("Fix login page", WebUtility.HtmlDecode(title.Groups["text"].Value));
    }

    [Fact]
    public async Task MobileMoveButtonShowsMoveTextAndKeepsStatusInName()
    {
        var html = await Render(Task(3, "Mobile task", "in_qa"), true, mobile: true);
        var (_, text) = MoveButton(html);

        Assert.Contains(">Move</span>", text);
        Assert.Contains("Status: In QA", text);
        Assert.Matches("card-row-title\"[^>]*>\\s*<h3[^>]*>Mobile task</h3>\\s*<div class=\"status-menu\"", html);
    }

    [Fact]
    public async Task TaskCardShowsOverdueTextWithDayCount()
    {
        var late = await Render(Task(1, "Late", "in_qa", due: new DateOnly(2026, 11, 4), overdueDays: 6), false);
        var fine = await Render(Task(2, "Fine", "in_qa", due: new DateOnly(2026, 11, 20)), false);

        Assert.Contains("Overdue by 6 days", late);
        Assert.Contains("Overdue · 6d", late);
        Assert.Contains("Nov 4", late);
        Assert.DoesNotContain("Overdue", fine);
        Assert.Contains("Nov 20", fine);
        Assert.DoesNotContain("class=\"card overdue", late);
        Assert.DoesNotContain("class=\"card overdue", fine);
        Assert.Contains("class=\"card\"", late);
    }

    [Fact]
    public async Task TaskCardWithoutDueDateShowsNoDueDate()
    {
        var html = await Render(Task(1, "Open", "in_qa"), false);

        Assert.Contains("No due date", html);
    }

    [Fact]
    public async Task ReaderCardHasNoMoveButton()
    {
        var html = await Render(Task(7, "Reader task", "in_qa"), false);

        Assert.DoesNotContain("move-button", html);
        Assert.DoesNotContain("status-menu", html);
        Assert.DoesNotContain("Status:", html);
        Assert.DoesNotContain("Item.Condition", html);
        Assert.Contains("Reader task", html);
        Assert.Contains("tabindex=\"0\"", html);
    }

    [Fact]
    public async Task UnassignedCardShowsUnassignedAndNoAvatarInitials()
    {
        var html = await Render(Task(1, "Nobody", "new_task"), false);

        Assert.Contains("Unassigned", html);
        Assert.Contains("avatar-empty", html);
        Assert.DoesNotContain("avatar-tone", html);
    }

    [Theory]
    [InlineData(3, 3)]
    [InlineData(8, 0)]
    [InlineData(15, 7)]
    public async Task CardAvatarUsesToneOfExecutorId(long id, int tone)
    {
        var html = await Render(Task(1, "Owned", "new_task", User(id, "ann", "Ann", "Lee")), false);

        Assert.Contains($"avatar avatar-card avatar-tone-{tone}\" aria-hidden=\"true\">AL</span>", html);
        Assert.Contains("Ann Lee", html);
    }

    private static Task<string> RenderOptions(string condition) =>
        RenderComponent<StatusOptions>(new() { ["TaskId"] = 5L, ["Condition"] = condition });

    [Fact]
    public async Task StatusOptionsRenderSevenOptionsWithDividerBeforeLast()
    {
        var html = await RenderOptions("in_qa");

        Assert.Equal(7, Regex.Matches(html, "role=\"option\"").Count);
        Assert.Single(Regex.Matches(html, "status-divider"));
        Assert.True(html.IndexOf("status-divider", StringComparison.Ordinal) < html.IndexOf("status-option-5-archived", StringComparison.Ordinal));
        Assert.Contains("Archive…", html);
        Assert.DoesNotContain(">Archived<", html);
        Assert.Single(Regex.Matches(html, "aria-selected=\"true\""));
        Assert.Equal(2, Regex.Matches(html, "<svg").Count);
        Assert.Matches("status-option-5-in_qa\"[^>]*aria-selected=\"true\"", html);
    }

    [Fact]
    public async Task StatusOptionsForArchivedTaskShowArchivedAsCurrent()
    {
        var html = await RenderOptions("archived");

        Assert.DoesNotContain("Archive…", html);
        Assert.Matches("status-option-5-archived\"[^>]*aria-selected=\"true\"", html);
        Assert.Contains(">Archived</span>", html);
        Assert.Single(Regex.Matches(html, "aria-selected=\"true\""));
    }

    [Fact]
    public async Task StatusOptionsMarkOnlyCurrentStatusWithCheckMark()
    {
        var html = await RenderOptions("released");
        var checks = Regex.Matches(html, "M3\\.5 8\\.5l3 3").Count;

        Assert.Equal(1, checks);
        Assert.Matches("status-option-5-released\"[\\s\\S]*?M3\\.5 8\\.5l3 3[\\s\\S]*?</li>", html);
    }

    private static Task<string> RenderColumn(int count, bool loading, bool mobile = false) =>
        RenderComponent<BoardColumn>(new()
        {
            ["Status"] = "in_qa",
            ["Tasks"] = (IReadOnlyList<TaskResponse>)Enumerable.Range(1, count).Select(index => Task(index, $"T{index}", "in_qa")).ToList(),
            ["Loading"] = loading,
            ["Mobile"] = mobile,
            ["Card"] = (RenderFragment<TaskResponse>)(item => builder => builder.AddContent(0, $"card-{item.Id}"))
        });

    [Theory]
    [InlineData(3, "3 tasks")]
    [InlineData(1, "1 task")]
    public async Task BoardColumnShowsCountPillWithAccessibleText(int count, string text)
    {
        var html = await RenderColumn(count, false);

        Assert.Contains("count-pill", html);
        Assert.Contains($"<span class=\"visually-hidden\">{text}</span>", html);
        Assert.Contains($"<span aria-hidden=\"true\">{count}</span>", html);
        Assert.Matches("<h2 id=\"column-title-in_qa\"><span class=\"status-dot status-dot-in-qa\" aria-hidden=\"true\"></span> In QA</h2>", html);
    }

    [Fact]
    public async Task BoardColumnLoadingShowsTwoSkeletonsAndNoCount()
    {
        var html = await RenderColumn(0, true);

        Assert.Equal(2, Regex.Matches(html, "class=\"skeleton\"").Count);
        Assert.DoesNotContain("count-pill", html);
        Assert.DoesNotContain("No tasks", html);
    }

    [Fact]
    public async Task BoardColumnEmptyShowsNoTasks()
    {
        var html = await RenderColumn(0, false);

        Assert.Contains("<p class=\"column-note\">No tasks</p>", html);
        Assert.Contains("0 tasks", html);
    }

    [Fact]
    public async Task BoardColumnMobileShowsGroupCountAsPlainText()
    {
        var html = await RenderColumn(2, false, mobile: true);

        Assert.Contains("class=\"group\"", html);
        Assert.Contains("<span class=\"group-count\">2</span>", html);
        Assert.DoesNotContain("count-pill", html);
        Assert.Contains("card-1", html);
    }

    [Fact]
    public async Task NoCardComponentRendersLiveRegion()
    {
        var card = await Render(Task(1, "A", "in_qa"), true);
        var options = await RenderOptions("in_qa");
        var column = await RenderColumn(1, false);
        var toolbar = await RenderToolbar(new BoardFilter(), 1, false);
        var all = card + options + column + toolbar;

        Assert.DoesNotContain("role=\"status\"", all);
        Assert.DoesNotContain("aria-live", all);
    }

    private static Task<string> RenderToolbar(BoardFilter filter, int overdueCount, bool mobile) =>
        RenderComponent<BoardToolbar>(new()
        {
            ["Filter"] = filter,
            ["Executors"] = BoardRules.ExecutorOptions([Task(1, "A", "new_task", User(4, "zed", "Zed", "Last"))]),
            ["OverdueCount"] = overdueCount,
            ["AllCount"] = 5,
            ["StatusCounts"] = (IReadOnlyDictionary<string, int>)TaskStatuses.All.ToDictionary(status => status, _ => 2),
            ["Mobile"] = mobile
        });

    [Fact]
    public async Task BoardToolbarRendersControlsAndOverdueButtonDisabledWhenZero()
    {
        var html = await RenderToolbar(new BoardFilter(), 0, false);

        Assert.Contains("type=\"search\"", html);
        Assert.Contains("aria-label=\"Search tasks\"", html);
        Assert.Contains("placeholder=\"Search tasks\"", html);
        Assert.Contains("aria-label=\"Executor\"", html);
        Assert.Contains(">All executors</option>", html);
        Assert.Contains(">Unassigned</option>", html);
        Assert.Contains(">Zed Last</option>", html);
        Assert.Matches("<button[^>]*class=\"overdue-toggle\"[^>]*aria-pressed=\"false\"[^>]*disabled", html);
        Assert.Contains("Overdue 0", html);
        Assert.Matches("class=\"help-button\"[^>]*aria-label=\"Keyboard shortcuts\"[^>]*aria-expanded=\"false\"[^>]*aria-controls=\"shortcuts-panel\"", html);
        Assert.DoesNotContain("shortcuts-panel\" class", html);
    }

    [Fact]
    public async Task BoardToolbarKeepsPressedOverdueButtonEnabledAtZero()
    {
        var html = await RenderToolbar(new BoardFilter(OverdueOnly: true), 0, false);

        Assert.Matches("class=\"overdue-toggle\"[^>]*aria-pressed=\"true\"", html);
        Assert.DoesNotMatch("class=\"overdue-toggle\"[^>]*disabled", html);
    }

    [Fact]
    public async Task BoardToolbarHasNoLiveRegion()
    {
        var html = await RenderToolbar(new BoardFilter(), 3, false);

        Assert.DoesNotContain("role=\"status\"", html);
        Assert.DoesNotContain("aria-live", html);
    }

    [Fact]
    public async Task BoardToolbarMobileShowsChipsAndNoHelpButton()
    {
        var html = await RenderToolbar(new BoardFilter(Status: "in_qa"), 3, true);

        Assert.DoesNotContain("help-button", html);
        Assert.DoesNotContain("overdue-toggle", html);
        Assert.Contains("role=\"group\" aria-label=\"Filter by status\"", html);
        Assert.Equal(9, Regex.Matches(html, "class=\"chip[ \"]").Count);
        Assert.Contains(">All 5</button>", html);
        Assert.Contains(">Overdue 3</button>", html);
        Assert.Contains(">In development 2</button>", html);
        Assert.Matches("<button[^>]*class=\"chip\"[^>]*aria-pressed=\"true\"[^>]*>In QA 2</button>", html);
        Assert.Matches("<button[^>]*class=\"chip\"[^>]*aria-pressed=\"false\"[^>]*>All 5</button>", html);
    }

    [Fact]
    public async Task BoardToolbarMobileAllChipIsPressedWithoutOverdueAndStatus()
    {
        var html = await RenderToolbar(new BoardFilter(), 3, true);

        Assert.Matches("<button[^>]*class=\"chip\"[^>]*aria-pressed=\"true\"[^>]*>All 5</button>", html);
    }
}

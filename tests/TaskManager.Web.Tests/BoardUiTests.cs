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

    private static TaskResponse Task(long id, string title, string condition) =>
        new(id, title, null, Today, Today, null, condition, null, null, null, [], false, null);

    private static async Task<string> Render(TaskResponse item, bool isStaff)
    {
        var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<TaskCard>(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                ["Item"] = item,
                ["IsStaff"] = isStaff
            }))).ToHtmlString());
    }

    private static (string Attributes, string Text) StatusButton(string html)
    {
        var clean = Regex.Replace(html, "<!--.*?-->", "", RegexOptions.Singleline);
        var match = Regex.Match(clean, "<button(?<attributes>[^>]*class=\"status-button\"[^>]*)>(?<text>.*?)</button>", RegexOptions.Singleline);
        Assert.True(match.Success, "status button not found");
        return (match.Groups["attributes"].Value, WebUtility.HtmlDecode(match.Groups["text"].Value));
    }

    [Theory]
    [InlineData("new_task", "New")]
    [InlineData("in_development", "In development")]
    [InlineData("in_qa", "In QA")]
    [InlineData("in_code_review", "In code review")]
    [InlineData("ready_for_release", "Ready for release")]
    [InlineData("released", "Released")]
    [InlineData("archived", "Archived")]
    public async Task StaffStatusButtonShowsStatusName(string condition, string name)
    {
        var html = await Render(Task(1, "Some task", condition), true);

        Assert.Contains($"Status: {name}", StatusButton(html).Text);
        Assert.DoesNotContain("Item.Condition", html);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TaskCardShowsOverdueBadgeFromItemFlag(bool overdue)
    {
        var html = await Render(Task(1, "Late task", "in_qa") with { IsOverdue = overdue }, false);

        Assert.Equal(overdue, html.Contains("OVERDUE"));
        Assert.Equal(overdue, Regex.IsMatch(html, "class=\"card overdue\""));
    }

    [Fact]
    public async Task StatusButtonNameContainsStatusAndDescriptionIsTaskTitle()
    {
        var html = await Render(Task(42, "Fix login page", "in_qa"), true);
        var (attributes, text) = StatusButton(html);

        Assert.DoesNotContain("aria-label=", attributes);
        Assert.DoesNotContain("aria-labelledby=", attributes);
        Assert.Contains("In QA", text);
        Assert.Contains("aria-describedby=\"task-title-42\"", attributes);
        var title = Regex.Match(html, "id=\"task-title-42\"[^>]*>(?<text>.*?)</", RegexOptions.Singleline);
        Assert.True(title.Success);
        Assert.Equal("Fix login page", WebUtility.HtmlDecode(title.Groups["text"].Value));
    }

    [Fact]
    public async Task ReaderCardHasNoStatusButton()
    {
        var html = await Render(Task(7, "Reader task", "in_qa"), false);

        Assert.DoesNotContain("status-button", html);
        Assert.DoesNotContain("status-menu", html);
        Assert.DoesNotContain("Status:", html);
        Assert.DoesNotContain("Item.Condition", html);
        Assert.Contains("Reader task", html);
    }
}

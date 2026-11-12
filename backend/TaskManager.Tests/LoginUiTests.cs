using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TaskManager.Api.Ui.Components;
using Xunit;

namespace TaskManager.Tests;

public sealed class LoginUiTests
{
    private static async Task<string> Render(string username = "", string password = "", bool failed = false, bool visible = false, bool busy = false)
    {
        var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<LoginForm>(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                ["Username"] = username,
                ["Password"] = password,
                ["Failed"] = failed,
                ["PasswordVisible"] = visible,
                ["Busy"] = busy
            }))).ToHtmlString());
        return Regex.Replace(html, "<!--.*?-->", "", RegexOptions.Singleline);
    }

    private static string Tag(string html, string id) =>
        Regex.Match(html, "<input[^>]*id=\"" + id + "\"[^>]*>").Value;

    private static string StatusRegion(string html)
    {
        var match = Regex.Match(html, "<div role=\"status\"[^>]*>(?<inner>.*?)</div>\\s*<form", RegexOptions.Singleline);
        Assert.True(match.Success, "live region not found");
        return match.Groups["inner"].Value;
    }

    [Fact]
    public async Task LoginFormDefaultStateHasNoBannerAndOneLiveRegion()
    {
        var html = await Render();

        Assert.Single(Regex.Matches(html, "role=\"status\""));
        Assert.Single(Regex.Matches(html, "aria-live"));
        Assert.True(string.IsNullOrWhiteSpace(StatusRegion(html)));
        Assert.DoesNotContain("banner", html);
        var password = Tag(html, "login-password");
        Assert.Contains("type=\"password\"", password);
        Assert.DoesNotContain("aria-invalid", password);
    }

    [Fact]
    public async Task LoginFormFailedStateShowsBannerInsideSingleLiveRegion()
    {
        var html = await Render(failed: true);

        Assert.Single(Regex.Matches(html, "role=\"status\""));
        Assert.Single(Regex.Matches(html, "aria-live"));
        var region = StatusRegion(html);
        Assert.Contains("Incorrect username or password.", region);
        Assert.Matches("<svg[^>]*aria-hidden=\"true\"", region);
        Assert.Contains("aria-invalid=\"true\"", Tag(html, "login-password"));
        Assert.DoesNotContain("Sign-in failed", html);
    }

    [Fact]
    public async Task LoginFormToggleIsNonSubmitButtonWithPressedState()
    {
        var hidden = await Render();
        var toggle = Regex.Match(hidden, "<button type=\"button\"[^>]*class=\"input-toggle\"[^>]*>(?<text>[^<]*)</button>");

        Assert.True(toggle.Success);
        Assert.Equal("Show", toggle.Groups["text"].Value);
        Assert.Contains("aria-pressed=\"false\"", toggle.Value);
        Assert.Contains("aria-controls=\"login-password\"", toggle.Value);

        var shown = await Render(visible: true);
        var pressed = Regex.Match(shown, "<button type=\"button\"[^>]*class=\"input-toggle\"[^>]*>(?<text>[^<]*)</button>");

        Assert.Equal("Hide", pressed.Groups["text"].Value);
        Assert.Contains("aria-pressed=\"true\"", pressed.Value);
        Assert.Contains("type=\"text\"", Tag(shown, "login-password"));
    }

    [Fact]
    public async Task LoginFormLabelsAreAssociatedWithInputs()
    {
        var html = await Render();

        Assert.Matches("<label for=\"login-username\">Username</label>", html);
        Assert.Matches("<label for=\"login-password\">Password</label>", html);
        Assert.NotEqual("", Tag(html, "login-username"));
        Assert.NotEqual("", Tag(html, "login-password"));
    }

    [Fact]
    public async Task LoginFormKeepsUsernameAndEmptyPasswordValue()
    {
        var html = await Render("anna", "", failed: true);

        Assert.Contains("value=\"anna\"", Tag(html, "login-username"));
        Assert.DoesNotMatch("value=\"[^\"]+\"", Tag(html, "login-password"));
        Assert.DoesNotContain("placeholder", html);
        Assert.DoesNotContain("empty after failure", html);
    }

    [Fact]
    public async Task LoginFormShowsStaticTexts()
    {
        var html = WebUtility.HtmlDecode(await Render(busy: true));

        Assert.Matches("<h1[^>]*>Sign in</h1>", html);
        Assert.Matches("<button type=\"submit\"[^>]*>Sign in</button>", html);
        Assert.Contains("to continue to Task board", html);
        Assert.Contains("Trouble signing in? Contact your administrator.", html);
        Assert.Matches("<button type=\"submit\"[^>]*aria-disabled=\"true\"", html);
    }
}

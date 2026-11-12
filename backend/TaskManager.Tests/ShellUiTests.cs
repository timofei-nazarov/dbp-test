using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TaskManager.Api.Api;
using TaskManager.Api.Ui.Components;
using TaskManager.Api.Ui.Layout;
using TaskManager.Api.Ui.Pages;
using Xunit;

namespace TaskManager.Tests;

public sealed class ShellUiTests
{
    private static async Task<string> RenderTopBar(bool signedIn, UserResponse? profile)
    {
        var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<TopBar>(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                ["SignedIn"] = signedIn,
                ["Profile"] = profile
            }))).ToHtmlString());
        return WebUtility.HtmlDecode(Regex.Replace(html, "<!--.*?-->", "", RegexOptions.Singleline));
    }

    private static UserResponse Profile(string first, string last, string username) =>
        new(1, username, first, last, "", "developer", null);

    [Fact]
    public async Task TopBarShowsInitialsUsernameAndSignOut()
    {
        var html = await RenderTopBar(true, Profile("Anna", "Smith", "anna"));

        Assert.Contains("Task board", html);
        Assert.Matches("<span class=\"avatar\" aria-hidden=\"true\">AS</span>", html);
        Assert.Matches("<span class=\"topbar-username\">anna</span>", html);
        Assert.Matches("<button type=\"button\"[^>]*>Sign out</button>", html);
    }

    [Fact]
    public async Task TopBarWithoutProfileShowsOnlySignOut()
    {
        var html = await RenderTopBar(true, null);

        Assert.Contains("Sign out", html);
        Assert.DoesNotContain("avatar", html);
        Assert.DoesNotContain("topbar-username", html);
    }

    [Fact]
    public async Task TopBarWhenSignedOutShowsOnlyBrand()
    {
        var html = await RenderTopBar(false, null);

        Assert.Contains("Task board", html);
        Assert.DoesNotContain("Sign out", html);
        Assert.DoesNotContain("avatar", html);
    }

    [Fact]
    public async Task TopBarHasNoLiveRegion()
    {
        var html = await RenderTopBar(true, Profile("Anna", "Smith", "anna"));

        Assert.DoesNotContain("aria-live", html);
        Assert.DoesNotContain("role=\"status\"", html);
    }

    [Fact]
    public void LoginPageUsesLoginLayoutAndBoardUsesDefault()
    {
        var login = typeof(Login).GetCustomAttributes(typeof(LayoutAttribute), true).Cast<LayoutAttribute>().Single();

        Assert.Equal(typeof(LoginLayout), login.LayoutType);
        Assert.Empty(typeof(Board).GetCustomAttributes(typeof(LayoutAttribute), true));
    }

    [Fact]
    public void RoutesUseMainLayoutAsDefault()
    {
        var routes = File.ReadAllText(FindFile("Ui", "Routes.razor"));

        Assert.Contains("DefaultLayout=\"typeof(MainLayout)\"", routes);
        Assert.Contains("<LayoutView Layout=\"typeof(MainLayout)\">", routes);
    }

    private static string FindFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine([directory.FullName, "backend", "TaskManager.Api", .. parts]);
            if (File.Exists(candidate))
            {
                return candidate;
            }
            directory = directory.Parent;
        }
        throw new FileNotFoundException(Path.Combine(parts));
    }
}

using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using TaskManager.Web;
using Xunit;

namespace TaskManager.Web.Tests;

public sealed class UserSessionTests
{
    private sealed class StubHandler(string access) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($"{{\"access\":\"{access}\",\"refresh\":\"refresh-value\"}}", Encoding.UTF8, "application/json")
            });
    }

    private sealed class StubFactory(string access) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new StubHandler(access)) { BaseAddress = new Uri("http://localhost/") };
    }

    private sealed class RecordingJs : IJSRuntime
    {
        public List<(string Identifier, object?[] Arguments)> Calls { get; } = [];

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            Calls.Add((identifier, args ?? []));
            return ValueTask.FromResult(default(TValue)!);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            InvokeAsync<TValue>(identifier, args);
    }

    private static string StaffToken()
    {
        var token = new JwtSecurityToken(
            claims: [new Claim("is_staff", "true")],
            expires: DateTime.UtcNow.AddMinutes(10));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    [Fact]
    public async Task SignOutClearsSessionAndStoredRefreshToken()
    {
        var provider = new ServiceCollection().AddDataProtection().Services.BuildServiceProvider();
        var js = new RecordingJs();
        var storage = new ProtectedSessionStorage(js, provider.GetRequiredService<IDataProtectionProvider>());
        var session = new UserSession(new StubFactory(StaffToken()), storage, TimeProvider.System);

        Assert.True(await session.SignInAsync("anna", "secret"));
        Assert.True(session.IsSignedIn);
        Assert.True(session.IsStaff);

        await session.SignOutAsync();

        Assert.False(session.IsSignedIn);
        Assert.False(session.IsStaff);
        var removal = Assert.Single(js.Calls, call => call.Identifier == "sessionStorage.removeItem");
        Assert.Contains("refresh_token", removal.Arguments);
    }
}

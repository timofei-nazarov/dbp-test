using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using TaskManager.Contracts;
using TaskManager.Web;
using Xunit;

namespace TaskManager.Web.Tests;

public sealed class TaskApiClientTests
{
    private const string TaskJson = """
        {"id":5,"title":"Task 5","description":null,"date_created":"2026-11-10","date_updated":"2026-11-10","date_to_finish":"2026-11-01","condition":"released","priority":null,"author":null,"executor":null,"tag":[],"is_overdue":false}
        """;

    private static string AccessToken() =>
        new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            claims: [new Claim("is_staff", "true")],
            expires: DateTime.UtcNow.AddMinutes(10)));

    private sealed class StubHandler(HttpStatusCode patchStatus) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Post && path.EndsWith("api/token/"))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent($"{{\"access\":\"{AccessToken()}\",\"refresh\":\"refresh-value\"}}", Encoding.UTF8, "application/json")
                });
            }
            if (request.Method == HttpMethod.Patch && path.EndsWith("api/tasks/5/"))
            {
                return Task.FromResult(new HttpResponseMessage(patchStatus)
                {
                    Content = new StringContent(patchStatus == HttpStatusCode.OK ? TaskJson : "{}", Encoding.UTF8, "application/json")
                });
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    private sealed class StubFactory(HttpStatusCode patchStatus) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new StubHandler(patchStatus)) { BaseAddress = new Uri("http://localhost/") };
    }

    private sealed class NullJs : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => ValueTask.FromResult(default(TValue)!);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            InvokeAsync<TValue>(identifier, args);
    }

    private static async Task<TaskApiClient> SignedInClient(HttpStatusCode patchStatus)
    {
        var provider = new ServiceCollection().AddDataProtection().Services.BuildServiceProvider();
        var storage = new ProtectedSessionStorage(new NullJs(), provider.GetRequiredService<IDataProtectionProvider>());
        var factory = new StubFactory(patchStatus);
        var session = new UserSession(factory, storage, TimeProvider.System);
        Assert.True(await session.SignInAsync("anna", "secret"));
        return new TaskApiClient(factory, session);
    }

    [Fact]
    public async Task SetConditionReturnsServerTaskWithRecomputedOverdueFlag()
    {
        var client = await SignedInClient(HttpStatusCode.OK);

        var (result, task) = await client.SetConditionAsync(5, "released");

        Assert.Equal(SaveResult.Ok, result);
        Assert.False(task!.IsOverdue);
        Assert.Equal("released", task.Condition);
    }

    [Fact]
    public async Task SetConditionReturnsForbiddenWithoutTask()
    {
        var client = await SignedInClient(HttpStatusCode.Forbidden);

        var (result, task) = await client.SetConditionAsync(5, "released");

        Assert.Equal(SaveResult.Forbidden, result);
        Assert.Null(task);
    }
}

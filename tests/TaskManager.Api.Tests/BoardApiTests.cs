using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace TaskManager.Api.Tests;

[Collection("api")]
public sealed class BoardApiTests(ApiFixture fixture)
{
    private static async Task<JsonObject> ObjectAsync(HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == expected, $"Expected {expected}, received {response.StatusCode}: {body}");
        return JsonNode.Parse(body)!.AsObject();
    }

    private static string StaffClaim(string token) =>
        new JwtSecurityTokenHandler().ReadJwtToken(token).Claims.Single(x => x.Type == "is_staff").Value;

    [Fact]
    public async Task AccessTokenCarriesStaffClaim()
    {
        using var client = fixture.CreateClient();
        foreach (var (username, expected) in new[] { ($"staff-{fixture.Prefix}", "true"), ($"reader-{fixture.Prefix}", "false") })
        {
            var tokens = await ObjectAsync(await client.PostAsJsonAsync("/api/token/", new { username, password = fixture.Password }));
            Assert.Equal(expected, StaffClaim(tokens["access"]!.GetValue<string>()));
            var refresh = tokens["refresh"]!.GetValue<string>();
            var refreshed = await ObjectAsync(await client.PostAsJsonAsync("/api/token/refresh/", new { refresh }));
            Assert.Equal(expected, StaffClaim(refreshed["access"]!.GetValue<string>()));
        }
    }

    [Fact]
    public async Task StaffCanChangeConditionWithPatchAndReaderGets403()
    {
        var task = await ObjectAsync(await fixture.Staff.PostAsJsonAsync("/api/tasks/", new { title = fixture.Unique("board-task") }), HttpStatusCode.Created);
        var id = task["id"]!.GetValue<long>();
        var before = task["condition"]!.GetValue<string>();
        Assert.Equal(HttpStatusCode.Forbidden, (await fixture.Reader.PatchAsJsonAsync($"/api/tasks/{id}/", new { condition = "archived" })).StatusCode);
        Assert.Equal(before, (await ObjectAsync(await fixture.Staff.GetAsync($"/api/tasks/{id}/")))["condition"]!.GetValue<string>());
        var patched = await ObjectAsync(await fixture.Staff.PatchAsJsonAsync($"/api/tasks/{id}/", new { condition = "archived" }));
        Assert.Equal("archived", patched["condition"]!.GetValue<string>());
    }

    [Fact]
    public async Task StaffAndReaderCanReadCurrentUserNames()
    {
        foreach (var (client, username) in new[] { (fixture.Staff, $"staff-{fixture.Prefix}"), (fixture.Reader, $"reader-{fixture.Prefix}") })
        {
            var current = await ObjectAsync(await client.GetAsync("/api/current-user/"));
            Assert.Equal(username, current["username"]!.GetValue<string>());
            Assert.Equal(System.Text.Json.JsonValueKind.String, current["first_name"]!.GetValueKind());
            Assert.Equal(System.Text.Json.JsonValueKind.String, current["last_name"]!.GetValueKind());
        }
    }

    [Fact]
    public async Task PatchWithUnknownConditionReturnsFieldError()
    {
        var task = await ObjectAsync(await fixture.Staff.PostAsJsonAsync("/api/tasks/", new { title = fixture.Unique("board-task") }), HttpStatusCode.Created);
        var error = await ObjectAsync(await fixture.Staff.PatchAsJsonAsync($"/api/tasks/{task["id"]!.GetValue<long>()}/", new { condition = "unknown" }), HttpStatusCode.BadRequest);
        Assert.NotEmpty(error["condition"]!.AsArray());
    }

    [Fact]
    public async Task BoardAndLoginPagesAreServedWithoutToken()
    {
        using var client = fixture.CreateClient();
        string? script = null;
        foreach (var path in new[] { "/", "/board", "/login" })
        {
            var response = await client.GetAsync(path);
            var html = await response.Content.ReadAsStringAsync();
            Assert.True(response.StatusCode == HttpStatusCode.OK, $"{path}: {response.StatusCode}: {html}");
            Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
            var match = System.Text.RegularExpressions.Regex.Match(html, "<script[^>]*src=\"([^\"]*blazor\\.web[^\"]*\\.js)\"");
            Assert.True(match.Success, $"{path}: no blazor.web script in {html}");
            script = match.Groups[1].Value;
        }
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(script)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/tasks/")).StatusCode);
    }
}

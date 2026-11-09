using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace TaskManager.Tests;

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
    public async Task PatchWithUnknownConditionReturnsFieldError()
    {
        var task = await ObjectAsync(await fixture.Staff.PostAsJsonAsync("/api/tasks/", new { title = fixture.Unique("board-task") }), HttpStatusCode.Created);
        var error = await ObjectAsync(await fixture.Staff.PatchAsJsonAsync($"/api/tasks/{task["id"]!.GetValue<long>()}/", new { condition = "unknown" }), HttpStatusCode.BadRequest);
        Assert.NotEmpty(error["condition"]!.AsArray());
    }
}

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Configuration;
using MimeKit;
using TaskManager.Api.Data;
using TaskManager.Api.Domain;
using TaskManager.Api.Services;
using Xunit;

namespace TaskManager.Tests;

[Collection("api")]
public sealed class BackendTests(ApiFixture fixture)
{
    private static async Task<JsonObject> ObjectAsync(HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == expected, $"Expected {expected}, received {response.StatusCode}: {body}");
        return JsonNode.Parse(body)!.AsObject();
    }

    private async Task<JsonObject> CreateTaskAsync(object payload) =>
        await ObjectAsync(await fixture.Staff.PostAsJsonAsync("/api/tasks/", payload), HttpStatusCode.Created);

    private async Task<JsonObject> CreateTagAsync(object payload) =>
        await ObjectAsync(await fixture.Staff.PostAsJsonAsync("/api/tags/", payload), HttpStatusCode.Created);

    [Fact]
    public async Task AuthenticationRequiresAnAccessToken()
    {
        using var client = fixture.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/tasks/")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/token/", new { username = "missing", password = "invalid" })).StatusCode);
        var tokens = await ObjectAsync(await client.PostAsJsonAsync("/api/token/", new { username = $"staff-{fixture.Prefix}", password = fixture.Password }));
        var refresh = tokens["refresh"]!.GetValue<string>();
        Assert.NotNull((await ObjectAsync(await client.PostAsJsonAsync("/api/token/refresh/", new { refresh })))["access"]);
        client.DefaultRequestHeaders.Authorization = new("Bearer", refresh);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/tasks/")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/token/refresh/", new { refresh = tokens["access"]!.GetValue<string>() })).StatusCode);
    }

    [Fact]
    public async Task TokenLifetimesRemainFiveMinutesAndOneDay()
    {
        using var client = fixture.CreateClient();
        var tokens = await ObjectAsync(await client.PostAsJsonAsync("/api/token/", new { username = $"staff-{fixture.Prefix}", password = fixture.Password }));
        var refresh = tokens["refresh"]!.GetValue<string>();
        client.DefaultRequestHeaders.Authorization = new("Bearer", tokens["access"]!.GetValue<string>());
        try
        {
            fixture.Clock.Offset = TimeSpan.FromMinutes(6);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/tasks/")).StatusCode);
            fixture.Clock.Offset = TimeSpan.FromHours(23) + TimeSpan.FromMinutes(59);
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/token/refresh/", new { refresh })).StatusCode);
            fixture.Clock.Offset = TimeSpan.FromDays(1) + TimeSpan.FromSeconds(1);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/token/refresh/", new { refresh })).StatusCode);
        }
        finally { fixture.Clock.Offset = TimeSpan.Zero; }
    }

    [Fact]
    public async Task StaffFlagControlsWritesRegardlessOfRole()
    {
        Assert.Equal(HttpStatusCode.OK, (await fixture.Reader.GetAsync("/api/tasks/")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await fixture.Reader.GetAsync("/api/users/")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await fixture.Reader.PostAsJsonAsync("/api/tags/", new { title = "blocked" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await fixture.Reader.PatchAsJsonAsync("/api/current-user/", new { first_name = "blocked" })).StatusCode);
        var tag = await CreateTagAsync(new { title = fixture.Unique("staff-tag") });
        Assert.Equal(HttpStatusCode.Forbidden, (await fixture.Reader.DeleteAsync($"/api/tags/{tag["id"]}/")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await fixture.Staff.DeleteAsync($"/api/tags/{tag["id"]}/")).StatusCode);
    }

    [Fact]
    public async Task UserCrudCurrentUserAndNameFilterWork()
    {
        var username = fixture.Unique("user");
        var name = fixture.Unique("Name");
        var user = await ObjectAsync(await fixture.Staff.PostAsJsonAsync("/api/users/", new { username, first_name = name, password = "ignored", is_staff = true }), HttpStatusCode.Created);
        Assert.Equal("developer", user["role"]!.GetValue<string>());
        Assert.False(user.ContainsKey("password"));
        var id = user["id"]!.GetValue<long>();
        var result = await fixture.Reader.GetFromJsonAsync<JsonArray>($"/api/users/?name={name.ToUpperInvariant()}");
        Assert.Single(result!);
        Assert.Equal(id, result![0]!["id"]!.GetValue<long>());
        await ObjectAsync(await fixture.Staff.PutAsJsonAsync($"/api/users/{id}/", new { username, first_name = "Updated" }));
        Assert.Equal("Updated", (await ObjectAsync(await fixture.Reader.GetAsync($"/api/users/{id}/")))["first_name"]!.GetValue<string>());
        var current = await ObjectAsync(await fixture.Staff.GetAsync("/api/current-user/"));
        Assert.Equal(fixture.StaffId, current["id"]!.GetValue<long>());
        Assert.Equal(fixture.StaffId, (await ObjectAsync(await fixture.Staff.GetAsync("/api/current-user/ignored/")))["id"]!.GetValue<long>());
        Assert.Equal("Current", (await ObjectAsync(await fixture.Staff.PatchAsJsonAsync("/api/current-user/", new { first_name = "Current" })))["first_name"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.BadRequest, (await fixture.Staff.PostAsJsonAsync("/api/users/", new { username })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await fixture.Staff.DeleteAsync($"/api/users/{id}/")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await fixture.Staff.GetAsync($"/api/users/{id}/")).StatusCode);
    }

    [Fact]
    public async Task ApiCreatedUsersStillHaveNoPasswordOrStaffPrivileges()
    {
        var username = fixture.Unique("no-password");
        var user = await ObjectAsync(await fixture.Staff.PostAsJsonAsync("/api/users/", new { username, password = fixture.Password, is_staff = true }), HttpStatusCode.Created);
        using var scope = fixture.Services.CreateScope();
        var entity = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.FindAsync(user["id"]!.GetValue<long>());
        Assert.False(entity!.IsStaff);
        Assert.Empty(entity.PasswordHash);
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.CreateClient().PostAsJsonAsync("/api/token/", new { username, password = fixture.Password })).StatusCode);
    }

    [Fact]
    public async Task AvatarUploadKeepsSizeExtensionAndUrlRules()
    {
        using var data = new MultipartFormDataContent();
        data.Add(new StringContent(fixture.Unique("avatar")), "username");
        data.Add(new ByteArrayContent([1, 2, 3]), "avatar_picture", "picture.jpg");
        var user = await ObjectAsync(await fixture.Staff.PostAsync("/api/users/", data), HttpStatusCode.Created);
        var url = user["avatar_picture"]!.GetValue<string>();
        Assert.StartsWith("http://localhost/media/", url);
        Assert.Equal(new byte[] { 1, 2, 3 }, await fixture.Staff.GetByteArrayAsync(url));
        using var oversized = new MultipartFormDataContent();
        oversized.Add(new StringContent(fixture.Unique("large")), "username");
        oversized.Add(new ByteArrayContent(new byte[1048577]), "avatar_picture", "picture.jpg");
        Assert.Equal("Maximum size 1048576 exceeded.", (await ObjectAsync(await fixture.Staff.PostAsync("/api/users/", oversized), HttpStatusCode.BadRequest))["avatar_picture"]![0]!.GetValue<string>());
        using var wrong = new MultipartFormDataContent();
        wrong.Add(new StringContent(fixture.Unique("wrong")), "username");
        wrong.Add(new ByteArrayContent([1]), "avatar_picture", "picture.pdf");
        Assert.Contains("pdf", (await ObjectAsync(await fixture.Staff.PostAsync("/api/users/", wrong), HttpStatusCode.BadRequest))["avatar_picture"]![0]!.GetValue<string>());
    }

    [Fact]
    public async Task TaskCreationPreservesNestedRecordsAndReadOnlyDates()
    {
        var task = await CreateTaskAsync(new
        {
            title = fixture.Unique("task"),
            description = "Description",
            date_created = "2000-01-01",
            date_to_finish = "2030-01-01",
            priority = 1,
            author = new { username = fixture.Unique("author") },
            executor = new { username = fixture.Unique("executor") },
            tag = new[] { new { title = fixture.Unique("tag") } }
        });
        Assert.Equal("new_task", task["condition"]!.GetValue<string>());
        Assert.Equal(DateTime.UtcNow.ToString("yyyy-MM-dd"), task["date_created"]!.GetValue<string>());
        Assert.NotNull(task["author"]!["id"]);
        Assert.NotNull(task["executor"]!["id"]);
        Assert.Single(task["tag"]!.AsArray());
        var id = task["id"]!.GetValue<long>();
        var executorId = task["executor"]!["id"]!.GetValue<long>();
        Assert.Equal(task.ToJsonString(), (await ObjectAsync(await fixture.Reader.GetAsync($"/api/tasks/{id}/"))).ToJsonString());
        Assert.Equal(HttpStatusCode.OK, (await fixture.Reader.GetAsync($"/api/users/{executorId}/tasks/{id}/")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await fixture.Reader.GetAsync($"/api/users/{fixture.ReaderId}/tasks/{id}/")).StatusCode);
        Assert.Equal("released", (await ObjectAsync(await fixture.Staff.PatchAsJsonAsync($"/api/tasks/{id}/", new { condition = "released" })))["condition"]!.GetValue<string>());
        Assert.Equal("Renamed", (await ObjectAsync(await fixture.Staff.PutAsJsonAsync($"/api/tasks/{id}/", new { title = "Renamed" })))["title"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.NoContent, (await fixture.Staff.DeleteAsync($"/api/tasks/{id}/")).StatusCode);
    }

    [Fact]
    public async Task DeletingUsersSetsTaskReferencesToNull()
    {
        var task = await CreateTaskAsync(new { title = "orphan", author = new { username = fixture.Unique("author") }, executor = new { username = fixture.Unique("executor") } });
        var id = task["id"]!.GetValue<long>();
        Assert.Equal(HttpStatusCode.NoContent, (await fixture.Staff.DeleteAsync($"/api/users/{task["author"]!["id"]}/")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await fixture.Staff.DeleteAsync($"/api/users/{task["executor"]!["id"]}/")).StatusCode);
        var result = await ObjectAsync(await fixture.Staff.GetAsync($"/api/tasks/{id}/"));
        Assert.Null(result["author"]);
        Assert.Null(result["executor"]);
    }

    [Fact]
    public async Task UnchangedTaskUpdatesStillRefreshTheUpdateDate()
    {
        var task = await CreateTaskAsync(new { title = "unchanged" });
        var id = task["id"]!.GetValue<long>();
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Tasks.Where(x => x.Id == id).ExecuteUpdateAsync(x => x.SetProperty(t => t.DateUpdated, new DateOnly(2000, 1, 1)));
        }
        var result = await ObjectAsync(await fixture.Staff.PatchAsJsonAsync($"/api/tasks/{id}/", new { title = "unchanged" }));
        Assert.Equal(DateTime.UtcNow.ToString("yyyy-MM-dd"), result["date_updated"]!.GetValue<string>());
    }

    [Fact]
    public async Task TaskFiltersCombineAllTagsAndValidateChoices()
    {
        var first = fixture.Unique("first");
        var second = fixture.Unique("second");
        var task = await CreateTaskAsync(new { title = "filters", condition = "in_qa", author = new { username = fixture.Unique("author") }, executor = new { username = fixture.Unique("executor") }, tag = new[] { new { title = first }, new { title = second } } });
        var query = $"/api/tasks/?condition=in_qa&author={task["author"]!["id"]}&executor={task["executor"]!["id"]}&tag={first}&tag={second}";
        var tasks = await fixture.Reader.GetFromJsonAsync<JsonArray>(query);
        Assert.Single(tasks!);
        Assert.Equal(task["id"]!.GetValue<long>(), tasks![0]!["id"]!.GetValue<long>());
        Assert.Empty((await fixture.Reader.GetFromJsonAsync<JsonArray>(query.Replace("condition=in_qa", "condition=archived")))!);
        Assert.Equal(HttpStatusCode.BadRequest, (await fixture.Reader.GetAsync("/api/tasks/?condition=invalid")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await fixture.Reader.GetAsync("/api/tasks/?executor=invalid")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await fixture.Reader.GetAsync("/api/tasks/?tag=missing-tag")).StatusCode);
    }

    [Fact]
    public async Task NestedTagDeletionDeletesSharedTagGlobally()
    {
        var first = await CreateTaskAsync(new { title = "first" });
        var second = await CreateTaskAsync(new { title = "second" });
        var firstId = first["id"]!.GetValue<long>();
        var secondId = second["id"]!.GetValue<long>();
        var tag = await ObjectAsync(await fixture.Staff.PostAsJsonAsync($"/api/tasks/{firstId}/tags/", new { title = "shared" }), HttpStatusCode.Created);
        var tagId = tag["id"]!.GetValue<long>();
        Assert.Equal(HttpStatusCode.NotFound, (await fixture.Reader.GetAsync($"/api/tasks/{secondId}/tags/{tagId}/")).StatusCode);
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var entity = await db.Tasks.Include(x => x.Tags).SingleAsync(x => x.Id == secondId);
            entity.Tags.Add(await db.Tags.SingleAsync(x => x.Id == tagId));
            await db.SaveChangesAsync();
        }
        Assert.Equal("changed", (await ObjectAsync(await fixture.Staff.PatchAsJsonAsync($"/api/tasks/{firstId}/tags/{tagId}/", new { title = "changed" })))["title"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.NoContent, (await fixture.Staff.DeleteAsync($"/api/tasks/{firstId}/tags/{tagId}/")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await fixture.Reader.GetAsync($"/api/tags/{tagId}/")).StatusCode);
        Assert.Empty((await fixture.Reader.GetFromJsonAsync<JsonArray>($"/api/tasks/{secondId}/tags/"))!);
    }

    [Fact]
    public async Task TagsSupportUniqueNullableIdsAndCrud()
    {
        var uniqueId = Random.Shared.NextInt64();
        var tag = await CreateTagAsync(new { title = "unique", unique_id = uniqueId });
        Assert.Equal(HttpStatusCode.BadRequest, (await fixture.Staff.PostAsJsonAsync("/api/tags/", new { title = "duplicate", unique_id = uniqueId })).StatusCode);
        await CreateTagAsync(new { title = "nullable", unique_id = (long?)null });
        await CreateTagAsync(new { title = "nullable", unique_id = (long?)null });
        Assert.Equal("updated", (await ObjectAsync(await fixture.Staff.PutAsJsonAsync($"/api/tags/{tag["id"]}/", new { title = "updated" })))["title"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.NoContent, (await fixture.Staff.DeleteAsync($"/api/tags/{tag["id"]}/")).StatusCode);
    }

    [Theory]
    [InlineData("{}", "title")]
    [InlineData("{\"title\":\"\"}", "title")]
    [InlineData("{\"title\":\"valid\",\"condition\":\"invalid\"}", "condition")]
    [InlineData("{\"title\":\"valid\",\"priority\":-1}", "priority")]
    [InlineData("{\"title\":\"valid\",\"priority\":32768}", "priority")]
    [InlineData("{\"title\":\"valid\",\"priority\":1.5}", "priority")]
    [InlineData("{\"title\":\"valid\",\"date_to_finish\":\"wrong\"}", "date_to_finish")]
    public async Task TaskValidationUsesFieldErrorArrays(string payload, string field)
    {
        var response = await ObjectAsync(await fixture.Staff.PostAsync("/api/tasks/", new StringContent(payload, System.Text.Encoding.UTF8, "application/json")), HttpStatusCode.BadRequest);
        Assert.IsType<JsonArray>(response[field]);
    }

    [Fact]
    public async Task BackgroundJobsPersistAndExposeFilesFailuresAndUnknownIds()
    {
        var accepted = await ObjectAsync(await fixture.Reader.PostAsJsonAsync("/api/countdown/", new { seconds = 1 }), HttpStatusCode.Created);
        var id = Guid.Parse(accepted["task_id"]!.GetValue<string>());
        var worker = new JobWorker(fixture.Services.GetRequiredService<IServiceScopeFactory>(), NullLogger<JobWorker>.Instance);
        await worker.StartAsync(CancellationToken.None);
        try
        {
            var response = await WaitForJobAsync(id);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var location = response.Headers.Location!;
            Assert.Equal("test data", await fixture.Reader.GetStringAsync(location));
            var failure = await ObjectAsync(await fixture.Reader.PostAsJsonAsync("/api/countdown/", new { seconds = -1 }), HttpStatusCode.Created);
            var failed = await WaitForJobAsync(Guid.Parse(failure["task_id"]!.GetValue<string>()));
            var body = await ObjectAsync(failed);
            Assert.Equal("failure", body["status"]!.GetValue<string>());
            Assert.NotNull(body["errors"]!["non_field_errors"]);
            Assert.Equal(HttpStatusCode.NotFound, (await fixture.Reader.GetAsync($"/api/jobs/{Guid.NewGuid()}/")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await fixture.Reader.GetAsync("/api/jobs/invalid/")).StatusCode);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
            worker.Dispose();
        }
    }

    [Fact]
    public async Task AssignmentDoesNotAutomaticallyQueueEmail()
    {
        var task = await CreateTaskAsync(new { title = "assignment", executor = new { username = fixture.Unique("executor"), email = "executor@example.com" } });
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var id = task["id"]!.GetValue<long>();
        Assert.False(await db.Jobs.AnyAsync(x => x.Kind == "assignment" && x.Payload.Contains(id.ToString())));
    }

    [Fact]
    public async Task AssignmentEmailIsDeliveredThroughSmtp()
    {
        var task = await CreateTaskAsync(new { title = "Task <one>", executor = new { username = fixture.Unique("mail"), email = "executor@example.com" } });
        await using var smtp = new SmtpServer();
        using var scope = fixture.Services.CreateScope();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Email:Backend"] = "smtp",
            ["Email:Host"] = "127.0.0.1",
            ["Email:Port"] = smtp.Port.ToString(),
            ["Email:From"] = "sender@example.com"
        }).Build();
        var service = new EmailService(scope.ServiceProvider.GetRequiredService<AppDbContext>(), configuration, NullLogger<EmailService>.Instance);
        await service.SendAssignmentAsync(task["id"]!.GetValue<long>(), CancellationToken.None);
        var raw = await smtp.Message.Task.WaitAsync(TimeSpan.FromSeconds(5));
        using var bytes = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(raw));
        var message = await MimeMessage.LoadAsync(bytes);
        Assert.Equal("You've assigned a task.", message.Subject);
        Assert.Equal("executor@example.com", message.To.Mailboxes.Single().Address);
        Assert.Equal("sender@example.com", message.From.Mailboxes.Single().Address);
        Assert.Contains("Task &lt;one&gt;", message.HtmlBody);
    }

    [Fact]
    public async Task WorkersRecoverExpiredLeasesAndProcessQueuedEmails()
    {
        var job = new BackgroundJob
        {
            Payload = JsonSerializer.Serialize(new CountdownPayload(0)),
            LeaseOwner = Guid.NewGuid(),
            LeaseUntil = DateTime.UtcNow.AddMinutes(-1)
        };
        var email = new BackgroundJob
        {
            Kind = "email",
            Payload = JsonSerializer.Serialize(new EmailMessage("Test", "<p>Message</p>", ["test@example.com"]))
        };
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Jobs.AddRange(job, email);
            await db.SaveChangesAsync();
        }
        using var first = new JobWorker(fixture.Services.GetRequiredService<IServiceScopeFactory>(), NullLogger<JobWorker>.Instance);
        using var second = new JobWorker(fixture.Services.GetRequiredService<IServiceScopeFactory>(), NullLogger<JobWorker>.Instance);
        await first.StartAsync(CancellationToken.None);
        await second.StartAsync(CancellationToken.None);
        try
        {
            Assert.Equal(HttpStatusCode.Created, (await WaitForJobAsync(job.Id)).StatusCode);
            Assert.Equal("success", (await ObjectAsync(await WaitForJobAsync(email.Id), HttpStatusCode.Created))["status"]!.GetValue<string>());
        }
        finally
        {
            await first.StopAsync(CancellationToken.None);
            await second.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task DocumentationAndErrorEndpointWork()
    {
        using var client = fixture.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/swagger/")).StatusCode);
        var schema = await ObjectAsync(await client.GetAsync("/swagger.json"));
        Assert.NotNull(schema["paths"]!["/api/tasks"]);
        Assert.NotNull(schema["paths"]!["/api/tasks"]!["post"]!["requestBody"]);
        Assert.NotNull(schema["components"]!["securitySchemes"]!["Bearer"]);
        Assert.Equal(HttpStatusCode.InternalServerError, (await client.GetAsync("/rollbar-error")).StatusCode);
    }

    private async Task<HttpResponseMessage> WaitForJobAsync(Guid id)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        while (true)
        {
            var response = await fixture.Reader.GetAsync($"/api/jobs/{id}/", timeout.Token);
            var body = await response.Content.ReadFromJsonAsync<JsonObject>(timeout.Token);
            if (body?["status"]?.GetValue<string>() is "success" or "failure") return response;
            response.Dispose();
            await Task.Delay(100, timeout.Token);
        }
    }
}

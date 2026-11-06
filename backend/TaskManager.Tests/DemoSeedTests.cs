using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TaskManager.Api.Data;
using TaskManager.Api.Services;
using Xunit;

namespace TaskManager.Tests;

[Collection("api")]
public sealed class DemoSeedTests(ApiFixture fixture)
{
    private IConfiguration Configuration(string staffUsername) => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["DEMO_STAFF_USERNAME"] = staffUsername,
        ["DEMO_STAFF_PASSWORD"] = fixture.Password,
        ["DEMO_READER_USERNAME"] = $"seed-reader-{fixture.Prefix}",
        ["DEMO_READER_PASSWORD"] = fixture.Password,
        ["DEMO_DEVELOPER_USERNAME"] = $"seed-developer-{fixture.Prefix}",
        ["DEMO_DEVELOPER_PASSWORD"] = fixture.Password
    }).Build();

    [Fact]
    public async Task SeedIsRepeatableAndAccountsCanAuthenticateWithExpectedPermissions()
    {
        using var scope = fixture.Services.CreateScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<AppDbContext>();
        var username = $"seed-staff-{fixture.Prefix}";
        var seeder = ActivatorUtilities.CreateInstance<DemoSeeder>(services, Configuration(username));
        await seeder.SeedAsync();
        var counts = (await db.Users.CountAsync(), await db.Tasks.CountAsync(), await db.Tags.CountAsync());
        var hashes = await db.Users.Where(x => x.Username.StartsWith("seed-")).OrderBy(x => x.Id).Select(x => x.PasswordHash).ToListAsync();
        await seeder.SeedAsync();
        Assert.Equal(counts, (await db.Users.CountAsync(), await db.Tasks.CountAsync(), await db.Tags.CountAsync()));
        Assert.Equal(hashes, await db.Users.Where(x => x.Username.StartsWith("seed-")).OrderBy(x => x.Id).Select(x => x.PasswordHash).ToListAsync());
        Assert.Equal(7, await db.Tasks.CountAsync(x => x.Title.StartsWith("Demo ")));
        Assert.Equal(3, await db.Tags.CountAsync(x => x.Title.StartsWith("demo-")));
        using var staff = await fixture.LoginAsync(username);
        using var reader = await fixture.LoginAsync($"seed-reader-{fixture.Prefix}");
        using var developer = await fixture.LoginAsync($"seed-developer-{fixture.Prefix}");
        Assert.Equal(HttpStatusCode.OK, (await developer.GetAsync("/api/tasks/")).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await staff.PostAsJsonAsync("/api/tasks/", new { title = fixture.Unique("seed-write") })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.PostAsJsonAsync("/api/tasks/", new { title = "forbidden" })).StatusCode);
    }

    [Fact]
    public async Task InvalidConfigurationAndAccountConflictsLeaveDatabaseUnchanged()
    {
        using var scope = fixture.Services.CreateScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<AppDbContext>();
        var counts = (await db.Users.CountAsync(), await db.Tasks.CountAsync(), await db.Tags.CountAsync());
        var missing = ActivatorUtilities.CreateInstance<DemoSeeder>(services, new ConfigurationBuilder().Build());
        await Assert.ThrowsAsync<InvalidOperationException>(() => missing.SeedAsync());
        var config = Configuration($"seed-rollback-{fixture.Prefix}");
        config["DEMO_READER_USERNAME"] = $"reader-{fixture.Prefix}";
        var conflicting = ActivatorUtilities.CreateInstance<DemoSeeder>(services, config);
        await Assert.ThrowsAsync<InvalidOperationException>(() => conflicting.SeedAsync());
        Assert.Equal(counts, (await db.Users.CountAsync(), await db.Tasks.CountAsync(), await db.Tags.CountAsync()));
    }
}

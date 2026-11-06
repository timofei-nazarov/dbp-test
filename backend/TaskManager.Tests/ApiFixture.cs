using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TaskManager.Api.Data;
using TaskManager.Api.Domain;
using TaskManager.Api.Services;
using Xunit;

namespace TaskManager.Tests;

public sealed class TestClock : TimeProvider
{
    public TimeSpan Offset { get; set; }
    public override DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow.Add(Offset);
}

public sealed class ApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    public string Prefix { get; } = Guid.NewGuid().ToString("N");
    public string Password => "Migration-test-password-123!";
    public long StaffId { get; private set; }
    public long ReaderId { get; private set; }
    public TestClock Clock { get; } = new();
    public HttpClient Staff { get; private set; } = null!;
    public HttpClient Reader { get; private set; } = null!;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        if (Environment.GetEnvironmentVariable("TEST_DATABASE") is { } database)
            builder.UseSetting("ConnectionStrings:Database", database);
        else if (Environment.GetEnvironmentVariable("Database__Host") is null)
            throw new InvalidOperationException("TEST_DATABASE or Database__Host is required for the test database.");
        builder.UseSetting("Jwt:Key", "integration-test-key-at-least-thirty-two-bytes");
        builder.UseSetting("Storage:Root", Path.Combine(Path.GetTempPath(), "task-manager-tests", Prefix));
        builder.UseSetting("Email:Backend", "console");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
        });
    }

    public async Task InitializeAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        var staff = new User { Username = $"staff-{Prefix}", IsStaff = true, Role = "developer" };
        var reader = new User { Username = $"reader-{Prefix}", Role = "admin" };
        staff.PasswordHash = hasher.HashPassword(staff, Password);
        reader.PasswordHash = hasher.HashPassword(reader, Password);
        db.Users.AddRange(staff, reader);
        await db.SaveChangesAsync();
        StaffId = staff.Id;
        ReaderId = reader.Id;
        Staff = await LoginAsync(staff.Username);
        Reader = await LoginAsync(reader.Username);
    }

    public async Task<HttpClient> LoginAsync(string username)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/token/", new { username, password = Password });
        response.EnsureSuccessStatusCode();
        var tokens = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens["access"]!.GetValue<string>());
        return client;
    }

    public string Unique(string name) => $"{name}-{Guid.NewGuid():N}";

    async Task IAsyncLifetime.DisposeAsync()
    {
        Staff.Dispose();
        Reader.Dispose();
        await DisposeAsync();
    }
}

[CollectionDefinition("api")]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>;

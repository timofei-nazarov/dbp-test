using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TaskManager.Contracts;
using TaskManager.Domain;
using TaskManager.Infrastructure.Data;

namespace TaskManager.Api.Demo;

public sealed class DemoSeeder(AppDbContext db, IConfiguration configuration, IPasswordHasher<User> hasher, ILogger<DemoSeeder> logger)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var accounts = new[]
        {
            Account("STAFF", "admin", true, "Morgan"),
            Account("READER", "manager", false, "Alex"),
            Account("DEVELOPER", "developer", false, "Sam")
        };
        if (accounts.Select(x => x.Username).Distinct().Count() != accounts.Length)
            throw new InvalidOperationException("Demo usernames must be distinct.");

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(76110601)", cancellationToken);
        var users = new List<User>();
        foreach (var account in accounts)
        {
            var user = await db.Users.SingleOrDefaultAsync(x => x.Username == account.Username, cancellationToken);
            if (user is null)
            {
                user = new User
                {
                    Username = account.Username,
                    FirstName = account.FirstName,
                    LastName = "Demo",
                    Email = $"{account.Key.ToLowerInvariant()}@demo.example.test",
                    Role = account.Role,
                    IsStaff = account.IsStaff
                };
                user.PasswordHash = hasher.HashPassword(user, account.Password);
                db.Users.Add(user);
            }
            else if (user.Email != $"{account.Key.ToLowerInvariant()}@demo.example.test"
                || user.IsStaff != account.IsStaff || !user.IsActive
                || string.IsNullOrEmpty(user.PasswordHash)
                || hasher.VerifyHashedPassword(user, user.PasswordHash, account.Password) == PasswordVerificationResult.Failed)
            {
                throw new InvalidOperationException($"Demo account {account.Username} conflicts with an existing account. No data was changed.");
            }
            users.Add(user);
        }

        var tags = new List<Tag>();
        foreach (var title in new[] { "demo-backend", "demo-bug", "demo-release" })
        {
            var tag = await db.Tags.SingleOrDefaultAsync(x => x.Title == title, cancellationToken);
            if (tag is null)
            {
                tag = new Tag { Title = title };
                db.Tags.Add(tag);
            }
            tags.Add(tag);
        }

        var tasks = new List<WorkTask>();
        for (var index = 0; index < TaskStatuses.All.Count; index++)
        {
            var title = $"Demo {index + 1}: {TaskStatuses.All[index]}";
            var task = await db.Tasks.SingleOrDefaultAsync(x => x.Title == title, cancellationToken);
            if (task is null)
            {
                task = new WorkTask
                {
                    Title = title,
                    Description = $"Demo API scenario for {TaskStatuses.All[index]}.",
                    Condition = TaskStatuses.All[index],
                    Author = users[0],
                    Executor = index == 0 ? null : users[index % 2 + 1],
                    Priority = index == 0 ? null : (short)index,
                    DateToFinish = index == 0 ? null : DateOnly.FromDateTime(DateTime.UtcNow).AddDays(index - 3),
                    Tags = index == 6 ? [] : index < 3 ? [tags[0], tags[1]] : [tags[0], tags[2]]
                };
                db.Tasks.Add(task);
            }
            tasks.Add(task);
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        foreach (var user in users) logger.LogInformation("Demo user {Username}: id={Id}, staff={IsStaff}", user.Username, user.Id, user.IsStaff);
        foreach (var task in tasks) logger.LogInformation("Demo task {Title}: id={Id}", task.Title, task.Id);
        foreach (var tag in tags) logger.LogInformation("Demo tag {Title}: id={Id}", tag.Title, tag.Id);
    }

    private DemoAccount Account(string key, string role, bool isStaff, string firstName)
    {
        var username = configuration[$"DEMO_{key}_USERNAME"];
        var password = configuration[$"DEMO_{key}_PASSWORD"];
        if (string.IsNullOrWhiteSpace(username) || username.Length > 150
            || !System.Text.RegularExpressions.Regex.IsMatch(username, @"\A[\w.@+\-]+\z")
            || string.IsNullOrEmpty(password) || password.Length < 8)
            throw new InvalidOperationException($"DEMO_{key}_USERNAME must be a valid username and DEMO_{key}_PASSWORD must contain at least eight characters.");
        return new(key, username, password, role, isStaff, firstName);
    }

    private sealed record DemoAccount(string Key, string Username, string Password, string Role, bool IsStaff, string FirstName);
}

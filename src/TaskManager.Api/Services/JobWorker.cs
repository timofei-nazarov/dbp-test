using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TaskManager.Domain;
using TaskManager.Infrastructure.Data;
using TaskManager.Infrastructure.Services;

namespace TaskManager.Api.Services;

public sealed record CountdownPayload(int Seconds);
public sealed record AssignmentPayload(long TaskId);

public sealed class JobWorker(IServiceScopeFactory scopes, ILogger<JobWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var job = await ClaimAsync(stoppingToken);
                if (job is null) await Task.Delay(250, stoppingToken);
                else await ProcessAsync(job, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Background worker failed; retrying");
                try { await Task.Delay(1000, stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    private async Task<BackgroundJob?> ClaimAsync(CancellationToken cancellationToken)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var job = (await db.Jobs.FromSqlRaw("SELECT * FROM \"Jobs\" WHERE \"Status\" = 'started' AND (\"LeaseUntil\" IS NULL OR \"LeaseUntil\" < NOW()) ORDER BY \"CreatedAt\" LIMIT 1 FOR UPDATE SKIP LOCKED").ToListAsync(cancellationToken)).SingleOrDefault();
        if (job is not null)
        {
            job.LeaseOwner = Guid.NewGuid();
            job.LeaseUntil = DateTime.UtcNow.AddSeconds(30);
            await db.SaveChangesAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return job;
    }

    private async Task ProcessAsync(BackgroundJob job, CancellationToken stoppingToken)
    {
        using var heartbeatStop = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var heartbeat = RenewLeaseAsync(job, heartbeatStop.Token);
        try
        {
            using var scope = scopes.CreateScope();
            var services = scope.ServiceProvider;
            string? result = null;
            switch (job.Kind)
            {
                case "countdown":
                    var countdown = JsonSerializer.Deserialize<CountdownPayload>(job.Payload)!;
                    if (countdown.Seconds < 0) throw new ArgumentException("sleep length must be non-negative");
                    await Task.Delay(TimeSpan.FromSeconds(countdown.Seconds), stoppingToken);
                    result = await services.GetRequiredService<FileStorage>().SaveReportAsync(job.Id, stoppingToken);
                    break;
                case "assignment":
                    var assignment = JsonSerializer.Deserialize<AssignmentPayload>(job.Payload)!;
                    await services.GetRequiredService<EmailService>().SendAssignmentAsync(assignment.TaskId, stoppingToken);
                    break;
                case "email":
                    await services.GetRequiredService<EmailService>().SendAsync(JsonSerializer.Deserialize<EmailMessage>(job.Payload)!, stoppingToken);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown job kind: {job.Kind}");
            }
            await CompleteAsync(job, "success", result, null, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Job {JobId} failed", job.Id);
            await CompleteAsync(job, "failure", null, exception.Message, stoppingToken);
        }
        finally
        {
            await heartbeatStop.CancelAsync();
            try { await heartbeat; }
            catch (OperationCanceledException) { }
        }
    }

    private async Task RenewLeaseAsync(BackgroundJob job, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Jobs.Where(x => x.Id == job.Id && x.LeaseOwner == job.LeaseOwner && x.Status == "started")
                .ExecuteUpdateAsync(x => x.SetProperty(j => j.LeaseUntil, DateTime.UtcNow.AddSeconds(30)), cancellationToken);
        }
    }

    private async Task CompleteAsync(BackgroundJob job, string status, string? result, string? error, CancellationToken cancellationToken)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Jobs.Where(x => x.Id == job.Id && x.LeaseOwner == job.LeaseOwner)
            .ExecuteUpdateAsync(x => x.SetProperty(j => j.Status, status)
                .SetProperty(j => j.Result, result).SetProperty(j => j.Error, error)
                .SetProperty(j => j.LeaseUntil, (DateTime?)null), cancellationToken);
    }
}

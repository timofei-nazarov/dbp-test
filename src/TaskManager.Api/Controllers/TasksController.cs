using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskManager.Api.Auth;
using TaskManager.Api.Errors;
using TaskManager.Api.Requests;
using TaskManager.Api.Responses;
using TaskManager.Contracts;
using TaskManager.Domain;
using TaskManager.Infrastructure.Data;

namespace TaskManager.Api.Controllers;

[Authorize]
[ServiceFilter(typeof(StaffWriteFilter))]
[Route("api/tasks")]
public sealed class TasksController(AppDbContext db, EntityWriter writer, Representations representations) : ControllerBase
{
    private IQueryable<WorkTask> Query => db.Tasks.Include(x => x.Author).Include(x => x.Executor).Include(x => x.Tags).AsSplitQuery();

    [HttpGet("")]
    [HttpGet("/api/users/{userId:long}/tasks")]
    public async Task<IActionResult> List(long? userId, CancellationToken cancellationToken)
    {
        var query = await FilterAsync(Query, cancellationToken);
        if (userId.HasValue) query = query.Where(x => x.ExecutorId == userId);
        return Ok((await query.OrderBy(x => x.Id).ToListAsync(cancellationToken)).Select(representations.Task));
    }

    [HttpGet("{id:long}")]
    [HttpGet("/api/users/{userId:long}/tasks/{id:long}")]
    public async Task<IActionResult> Get(long id, long? userId, CancellationToken cancellationToken)
    {
        var query = await FilterAsync(Query, cancellationToken);
        if (userId.HasValue) query = query.Where(x => x.ExecutorId == userId);
        return Ok(representations.Task(await query.SingleOrDefaultAsync(x => x.Id == id, cancellationToken) ?? throw ApiException.NotFound()));
    }

    [HttpPost("")]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var task = await writer.TaskAsync(await RequestData.ReadAsync(Request), null, false, cancellationToken);
        db.Tasks.Add(task);
        await db.SaveChangesAsync(cancellationToken);
        return StatusCode(201, representations.Task(task));
    }

    [HttpPut("{id:long}")]
    [HttpPatch("{id:long}")]
    public async Task<IActionResult> Update(long id, CancellationToken cancellationToken)
    {
        var task = await writer.TaskAsync(await RequestData.ReadAsync(Request), await FindAsync(id, cancellationToken), Request.Method == "PATCH", cancellationToken);
        db.Entry(task).Property(x => x.DateUpdated).IsModified = true;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(representations.Task(task));
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        db.Tasks.Remove(await FindAsync(id, cancellationToken));
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private async Task<WorkTask> FindAsync(long id, CancellationToken cancellationToken) =>
        await (await FilterAsync(Query, cancellationToken)).SingleOrDefaultAsync(x => x.Id == id, cancellationToken) ?? throw ApiException.NotFound();

    private async Task<IQueryable<WorkTask>> FilterAsync(IQueryable<WorkTask> query, CancellationToken cancellationToken)
    {
        var filters = Request.Query;
        if (filters.TryGetValue("condition", out var condition) && !string.IsNullOrEmpty(condition))
        {
            if (!TaskStatuses.All.Contains(condition.ToString())) throw ApiException.Invalid("condition", "Select a valid choice. That choice is not one of the available choices.");
            query = query.Where(x => x.Condition == condition.ToString());
        }
        foreach (var key in new[] { "author", "executor" })
        {
            if (!filters.TryGetValue(key, out var value) || string.IsNullOrEmpty(value)) continue;
            if (!long.TryParse(value, out var id) || !await db.Users.AnyAsync(x => x.Id == id, cancellationToken))
                throw ApiException.Invalid(key, "Select a valid choice. That choice is not one of the available choices.");
            query = key == "author" ? query.Where(x => x.AuthorId == id) : query.Where(x => x.ExecutorId == id);
        }
        DateOnly? from = null, to = null;
        foreach (var key in new[] { "date_to_finish_from", "date_to_finish_to" })
        {
            var values = filters[key];
            if (values.Count > 1) throw ApiException.Invalid(key, "Date has wrong format. Use one of these formats instead: YYYY-MM-DD.");
            if (string.IsNullOrEmpty(values.ToString())) continue;
            var date = RequestData.ParseDate(key, values.ToString());
            if (key == "date_to_finish_from") from = date;
            else to = date;
        }
        if (from > to)
            throw ApiException.Invalid("date_to_finish_to", "Ensure this date is not earlier than date_to_finish_from.");
        if (from is { } start) query = query.Where(x => x.DateToFinish >= start);
        if (to is { } end) query = query.Where(x => x.DateToFinish <= end);
        foreach (var title in filters["tag"].Distinct())
        {
            if (string.IsNullOrEmpty(title)) continue;
            if (!await db.Tags.AnyAsync(x => x.Title == title, cancellationToken))
                throw ApiException.Invalid("tag", $"Select a valid choice. {title} is not one of the available choices.");
            query = query.Where(x => x.Tags.Any(t => t.Title == title));
        }
        return query;
    }
}

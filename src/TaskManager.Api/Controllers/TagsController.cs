using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskManager.Api.Auth;
using TaskManager.Api.Errors;
using TaskManager.Api.Requests;
using TaskManager.Api.Responses;
using TaskManager.Domain;
using TaskManager.Infrastructure.Data;

namespace TaskManager.Api.Controllers;

[Authorize]
[ServiceFilter(typeof(StaffWriteFilter))]
[Route("api/tags")]
public sealed class TagsController(AppDbContext db, EntityWriter writer) : ControllerBase
{
    [HttpGet("")]
    [HttpGet("/api/tasks/{taskId:long}/tags")]
    public async Task<IActionResult> List(long? taskId, CancellationToken cancellationToken) =>
        Ok((await (await QueryAsync(taskId, cancellationToken)).OrderBy(x => x.Id).ToListAsync(cancellationToken)).Select(Representations.Tag));

    [HttpGet("{id:long}")]
    [HttpGet("/api/tasks/{taskId:long}/tags/{id:long}")]
    public async Task<IActionResult> Get(long id, long? taskId, CancellationToken cancellationToken) =>
        Ok(Representations.Tag(await FindAsync(id, taskId, cancellationToken)));

    [HttpPost("")]
    [HttpPost("/api/tasks/{taskId:long}/tags")]
    public async Task<IActionResult> Create(long? taskId, CancellationToken cancellationToken)
    {
        WorkTask? task = taskId.HasValue
            ? await db.Tasks.Include(x => x.Tags).SingleOrDefaultAsync(x => x.Id == taskId, cancellationToken) ?? throw ApiException.NotFound()
            : null;
        var tag = await writer.TagAsync(await RequestData.ReadAsync(Request), null, false, cancellationToken);
        db.Tags.Add(tag);
        task?.Tags.Add(tag);
        await db.SaveChangesAsync(cancellationToken);
        return StatusCode(201, Representations.Tag(tag));
    }

    [HttpPut("{id:long}")]
    [HttpPatch("{id:long}")]
    [HttpPut("/api/tasks/{taskId:long}/tags/{id:long}")]
    [HttpPatch("/api/tasks/{taskId:long}/tags/{id:long}")]
    public async Task<IActionResult> Update(long id, long? taskId, CancellationToken cancellationToken)
    {
        var tag = await writer.TagAsync(await RequestData.ReadAsync(Request), await FindAsync(id, taskId, cancellationToken), Request.Method == "PATCH", cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(Representations.Tag(tag));
    }

    [HttpDelete("{id:long}")]
    [HttpDelete("/api/tasks/{taskId:long}/tags/{id:long}")]
    public async Task<IActionResult> Delete(long id, long? taskId, CancellationToken cancellationToken)
    {
        db.Tags.Remove(await FindAsync(id, taskId, cancellationToken));
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private async Task<IQueryable<Tag>> QueryAsync(long? taskId, CancellationToken cancellationToken)
    {
        if (!taskId.HasValue) return db.Tags;
        if (!await db.Tasks.AnyAsync(x => x.Id == taskId, cancellationToken)) throw ApiException.NotFound();
        return db.Tags.Where(x => x.Tasks.Any(t => t.Id == taskId));
    }

    private async Task<Tag> FindAsync(long id, long? taskId, CancellationToken cancellationToken) =>
        await (await QueryAsync(taskId, cancellationToken)).SingleOrDefaultAsync(x => x.Id == id, cancellationToken) ?? throw ApiException.NotFound();
}

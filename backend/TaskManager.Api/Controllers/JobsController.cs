using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskManager.Api.Api;
using TaskManager.Api.Data;
using TaskManager.Api.Domain;
using TaskManager.Api.Services;

namespace TaskManager.Api.Controllers;

[Authorize]
public sealed class JobsController(AppDbContext db, Representations representations) : ControllerBase
{
    [HttpPost("/api/countdown")]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var data = await RequestData.ReadAsync(Request);
        var seconds = data.Integer("seconds", true)!.Value;
        if (seconds is > int.MaxValue or < int.MinValue) throw ApiException.Invalid("seconds", "A valid integer is required.");
        var job = new BackgroundJob { Payload = JsonSerializer.Serialize(new CountdownPayload((int)seconds)) };
        db.Jobs.Add(job);
        await db.SaveChangesAsync(cancellationToken);
        Response.Headers.Location = $"/api/jobs/{job.Id}/";
        return StatusCode(201, new { task_id = job.Id.ToString(), status = "STARTED" });
    }

    [HttpGet("/api/jobs/{id}")]
    public async Task<IActionResult> Get(string id, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(id, out var key)) throw ApiException.NotFound();
        var job = await db.Jobs.AsNoTracking().SingleOrDefaultAsync(x => x.Id == key, cancellationToken) ?? throw ApiException.NotFound();
        if (job.Status != "success") return Ok(Representations.Job(job));
        Response.Headers.Location = representations.Absolute(job.Result ?? "");
        return StatusCode(201, Representations.Job(job));
    }
}

using System.Security.Claims;
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
[Route("api/users")]
public sealed class UsersController(AppDbContext db, EntityWriter writer, Representations representations) : ControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> List([FromQuery] string? name, CancellationToken cancellationToken)
    {
        var query = db.Users.AsQueryable();
        if (!string.IsNullOrEmpty(name)) query = query.Where(x => x.FirstName.ToUpper().Contains(name.ToUpper()));
        return Ok((await query.OrderBy(x => x.Id).ToListAsync(cancellationToken)).Select(representations.User));
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken cancellationToken) =>
        Ok(representations.User(await FindAsync(id, cancellationToken)));

    [HttpGet("/api/current-user")]
    [HttpGet("/api/current-user/{ignoredId}")]
    public Task<IActionResult> Current(CancellationToken cancellationToken) => Get(CurrentId, cancellationToken);

    [HttpPost("")]
    [HttpPost("/api/current-user")]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var user = await writer.UserAsync(await RequestData.ReadAsync(Request), null, false, cancellationToken);
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);
        return StatusCode(201, representations.User(user));
    }

    [HttpPut("{id:long}")]
    [HttpPatch("{id:long}")]
    public async Task<IActionResult> Update(long id, CancellationToken cancellationToken)
    {
        var user = await writer.UserAsync(await RequestData.ReadAsync(Request), await FindAsync(id, cancellationToken), Request.Method == "PATCH", cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(representations.User(user));
    }

    [HttpPut("/api/current-user")]
    [HttpPatch("/api/current-user")]
    [HttpPut("/api/current-user/{ignoredId}")]
    [HttpPatch("/api/current-user/{ignoredId}")]
    public Task<IActionResult> UpdateCurrent(CancellationToken cancellationToken) => Update(CurrentId, cancellationToken);

    [HttpDelete("/api/current-user/{ignoredId}")]
    public Task<IActionResult> DeleteCurrent(CancellationToken cancellationToken) => Delete(CurrentId, cancellationToken);

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        db.Users.Remove(await FindAsync(id, cancellationToken));
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private long CurrentId => long.Parse(User.FindFirstValue("user_id")!);

    private async Task<User> FindAsync(long id, CancellationToken cancellationToken) =>
        await db.Users.FindAsync([id], cancellationToken) ?? throw ApiException.NotFound();
}

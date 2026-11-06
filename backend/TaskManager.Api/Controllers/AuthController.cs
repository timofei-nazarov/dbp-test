using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskManager.Api.Api;
using TaskManager.Api.Data;
using TaskManager.Api.Domain;
using TaskManager.Api.Services;

namespace TaskManager.Api.Controllers;

[AllowAnonymous]
[Route("api/token")]
public sealed class AuthController(AppDbContext db, IPasswordHasher<User> hasher, TokenService tokens) : ControllerBase
{
    [HttpPost("")]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var data = await RequestData.ReadAsync(Request);
        var username = data.String("username", required: true, blank: false);
        var password = data.String("password", required: true, blank: false, trim: false);
        var user = await db.Users.SingleOrDefaultAsync(x => x.Username == username, cancellationToken);
        if (user is null || !user.IsActive || string.IsNullOrEmpty(user.PasswordHash)
            || hasher.VerifyHashedPassword(user, user.PasswordHash, password!) == PasswordVerificationResult.Failed)
            throw new ApiException(401, new { detail = "No active account found with the given credentials" });
        return Ok(new { refresh = tokens.Create(user, "refresh"), access = tokens.Create(user, "access") });
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        var data = await RequestData.ReadAsync(Request);
        var id = tokens.ValidateRefresh(data.String("refresh", required: true, blank: false)!);
        var user = await db.Users.FindAsync([id], cancellationToken);
        if (user is null || !user.IsActive)
            throw new ApiException(401, new { detail = "No active account found for the given token." });
        return Ok(new { access = tokens.Create(user, "access") });
    }
}

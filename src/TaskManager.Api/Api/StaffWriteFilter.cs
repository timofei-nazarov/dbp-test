using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using TaskManager.Infrastructure.Data;

namespace TaskManager.Api.Api;

public sealed class StaffWriteFilter(AppDbContext db) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var id = context.HttpContext.User.FindFirstValue("user_id");
        var user = long.TryParse(id, out var key) ? await db.Users.FindAsync(key) : null;
        if (user is null || !user.IsActive)
        {
            context.Result = new JsonResult(new { detail = "User not found", code = "user_not_found" }) { StatusCode = 401 };
            return;
        }
        var method = context.HttpContext.Request.Method;
        if (method is not ("GET" or "HEAD" or "OPTIONS") && !user.IsStaff)
        {
            context.Result = new JsonResult(new { detail = "You do not have permission to perform this action." }) { StatusCode = 403 };
            return;
        }
        await next();
    }
}

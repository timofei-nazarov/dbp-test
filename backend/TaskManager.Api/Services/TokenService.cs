using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using TaskManager.Api.Api;
using TaskManager.Api.Domain;

namespace TaskManager.Api.Services;

public sealed class TokenService(IConfiguration configuration, TimeProvider clock)
{
    public TokenValidationParameters ValidationParameters => new()
    {
        ValidateIssuer = true,
        ValidIssuer = configuration["Jwt:Issuer"] ?? "task-manager",
        ValidateAudience = true,
        ValidAudience = configuration["Jwt:Audience"] ?? "task-manager",
        ValidateIssuerSigningKey = true,
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!)),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero,
        LifetimeValidator = (before, expires, _, _) => expires.HasValue
            && expires.Value > clock.GetUtcNow().UtcDateTime
            && (!before.HasValue || before.Value <= clock.GetUtcNow().UtcDateTime)
    };

    public string Create(User user, string type)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var lifetime = type == "access" ? TimeSpan.FromMinutes(5) : TimeSpan.FromDays(1);
        var token = new JwtSecurityToken(
            issuer: ValidationParameters.ValidIssuer,
            audience: ValidationParameters.ValidAudience,
            claims: [new("user_id", user.Id.ToString()), new("token_type", type), new("is_staff", user.IsStaff ? "true" : "false"), new("jti", Guid.NewGuid().ToString("N"))],
            notBefore: now,
            expires: now.Add(lifetime),
            signingCredentials: new(ValidationParameters.IssuerSigningKey, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public long ValidateRefresh(string token)
    {
        try
        {
            var principal = new JwtSecurityTokenHandler().ValidateToken(token, ValidationParameters, out _);
            if (principal.FindFirstValue("token_type") != "refresh") throw new SecurityTokenException();
            return long.Parse(principal.FindFirstValue("user_id")!);
        }
        catch (Exception exception) when (exception is SecurityTokenException or ArgumentException or FormatException)
        {
            throw new ApiException(401, new { detail = "Token is invalid or expired", code = "token_not_valid" });
        }
    }
}

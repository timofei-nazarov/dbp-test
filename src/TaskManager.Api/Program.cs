using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using TaskManager.Api.Api;
using TaskManager.Api.Services;
using TaskManager.Domain;
using TaskManager.Infrastructure.Data;
using TaskManager.Infrastructure.Services;
using TaskManager.Web;

var builder = WebApplication.CreateBuilder(args);
var worker = args.Contains("--worker");
builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("Database")
    ?? new Npgsql.NpgsqlConnectionStringBuilder
    {
        Host = builder.Configuration["Database:Host"],
        Database = builder.Configuration["Database:Name"],
        Username = builder.Configuration["Database:Username"],
        Password = builder.Configuration["Database:Password"],
        GssEncryptionMode = Npgsql.GssEncryptionMode.Disable
    }.ConnectionString));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<TokenService>();
builder.Services.AddSingleton<FileStorage>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<EntityWriter>();
builder.Services.AddScoped<DemoSeeder>();
builder.Services.AddScoped<StaffWriteFilter>();
builder.Services.AddScoped<Representations>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddHttpClient<ErrorReporter>(client => client.Timeout = TimeSpan.FromSeconds(3));
builder.Services.AddHttpContextAccessor();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddHttpClient("ui-api", client => client.BaseAddress = new Uri((builder.Configuration["Ui:ApiBaseUrl"] ?? "http://localhost:8080").TrimEnd('/') + "/"));
builder.Services.AddScoped<UserSession>();
builder.Services.AddScoped<TaskApiClient>();
builder.Services.AddControllers().AddJsonOptions(options => options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo { Title = "Task Manager API", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.OpenApiSecurityScheme
    {
        Type = Microsoft.OpenApi.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    options.OperationFilter<ApiSchemaFilter>();
});
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme).Configure<TokenService>((options, tokens) =>
{
    options.MapInboundClaims = false;
    options.TokenValidationParameters = tokens.ValidationParameters;
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            if (context.Principal?.FindFirstValue("token_type") != "access")
            {
                context.Fail("Access token required.");
                return;
            }
            var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
            if (!long.TryParse(context.Principal.FindFirstValue("user_id"), out var id)
                || !await db.Users.AnyAsync(x => x.Id == id && x.IsActive)) context.Fail("User not found.");
        },
        OnChallenge = async context =>
        {
            context.HandleResponse();
            context.Response.StatusCode = 401;
            context.Response.Headers.WWWAuthenticate = "Bearer realm=\"api\"";
            await context.Response.WriteAsJsonAsync(new { detail = "Authentication credentials were not provided or are invalid." });
        }
    };
});
builder.Services.AddAuthorization();
if (worker) builder.Services.AddHostedService<JobWorker>();
var app = builder.Build();

if (args.Contains("--migrate"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    return;
}

if (args.Contains("--seed-demo"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<DemoSeeder>().SeedAsync();
    return;
}

if (args.Contains("--create-user"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var username = builder.Configuration["BOOTSTRAP_USERNAME"] ?? throw new InvalidOperationException("BOOTSTRAP_USERNAME is required.");
    var password = builder.Configuration["BOOTSTRAP_PASSWORD"] ?? throw new InvalidOperationException("BOOTSTRAP_PASSWORD is required.");
    if (string.IsNullOrWhiteSpace(username) || password.Length < 8) throw new InvalidOperationException("A username and a password of at least 8 characters are required.");
    if (await db.Users.AnyAsync(x => x.Username == username)) throw new InvalidOperationException("The username already exists.");
    var user = new User
    {
        Username = username,
        Email = builder.Configuration["BOOTSTRAP_EMAIL"] ?? "",
        IsStaff = builder.Configuration.GetValue("BOOTSTRAP_IS_STAFF", true),
        IsSuperuser = builder.Configuration.GetValue("BOOTSTRAP_IS_STAFF", true),
        Role = builder.Configuration["BOOTSTRAP_ROLE"] ?? "admin"
    };
    user.PasswordHash = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>().HashPassword(user, password);
    db.Users.Add(user);
    await db.SaveChangesAsync();
    app.Logger.LogInformation("Created user {Username}", username);
    return;
}

if (args.Contains("--send-assignment") || args.Contains("--send-email"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    BackgroundJob job;
    if (args.Contains("--send-assignment"))
    {
        var id = long.Parse(args[Array.IndexOf(args, "--send-assignment") + 1]);
        job = new() { Kind = "assignment", Payload = JsonSerializer.Serialize(new AssignmentPayload(id)) };
    }
    else
    {
        var path = args[Array.IndexOf(args, "--send-email") + 1];
        var email = JsonSerializer.Deserialize<EmailMessage>(await File.ReadAllTextAsync(path)) ?? throw new InvalidOperationException("Invalid email payload.");
        job = new() { Kind = "email", Payload = JsonSerializer.Serialize(email) };
    }
    db.Jobs.Add(job);
    await db.SaveChangesAsync();
    app.Logger.LogInformation("Queued job {JobId}", job.Id);
    return;
}

if (string.IsNullOrEmpty(builder.Configuration["Jwt:Key"]) || System.Text.Encoding.UTF8.GetByteCount(builder.Configuration["Jwt:Key"]!) < 32)
    throw new InvalidOperationException("Jwt:Key must contain at least 32 bytes.");

app.Use(async (context, next) =>
{
    var started = Stopwatch.GetTimestamp();
    try
    {
        await next(context);
    }
    catch (ApiException exception)
    {
        context.Response.StatusCode = exception.StatusCode;
        await context.Response.WriteAsJsonAsync(exception.Body);
    }
    catch (DbUpdateException exception) when (exception.InnerException is Npgsql.PostgresException { SqlState: "23505" })
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsJsonAsync(new { non_field_errors = new[] { "A record with these unique values already exists." } });
    }
    catch (Exception exception)
    {
        app.Logger.LogError(exception, "Unhandled request error");
        await context.RequestServices.GetRequiredService<ErrorReporter>().ReportAsync(exception, context);
        context.Response.StatusCode = 500;
        await context.Response.WriteAsJsonAsync(new { detail = "Internal server error." });
    }
    finally
    {
        app.Logger.LogInformation("{Method} {Path} status={Status} user={UserId} duration={Duration}ms", context.Request.Method,
            context.Request.Path, context.Response.StatusCode, context.User.FindFirstValue("user_id") ?? "-", Stopwatch.GetElapsedTime(started).TotalMilliseconds);
    }
});

var storage = app.Services.GetRequiredService<FileStorage>();
if (storage.IsLocal)
    app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(storage.Root), RequestPath = "/media", ServeUnknownFileTypes = true, DefaultContentType = "application/octet-stream" });
app.UseSwagger(options => options.RouteTemplate = "swagger/{documentName}/swagger.json");
app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "Task Manager API"));
app.MapGet("/swagger.json", () => Results.Redirect("/swagger/v1/swagger.json"));
app.MapGet("/redoc", () => Results.Content("<!DOCTYPE html><html><head><title>Task Manager API</title></head><body><redoc spec-url='/swagger/v1/swagger.json'></redoc><script src='https://cdn.redoc.ly/redoc/latest/bundles/redoc.standalone.js'></script></body></html>", "text/html"));
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/rollbar-error", () => { throw new InvalidOperationException("Generated diagnostic error."); });
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
if (!worker)
{
    app.MapControllers();
    app.MapStaticAssets();
    app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
}
await app.RunAsync();

public partial class Program;

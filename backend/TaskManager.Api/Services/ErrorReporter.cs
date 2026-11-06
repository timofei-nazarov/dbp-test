namespace TaskManager.Api.Services;

public sealed class ErrorReporter(HttpClient client, IConfiguration configuration, ILogger<ErrorReporter> logger)
{
    public async Task ReportAsync(Exception exception, HttpContext context)
    {
        var token = configuration["Rollbar:AccessToken"];
        if (string.IsNullOrWhiteSpace(token)) return;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.rollbar.com/api/1/item/");
            request.Headers.Add("X-Rollbar-Access-Token", token);
            request.Content = JsonContent.Create(new
            {
                data = new
                {
                    environment = configuration["Rollbar:Environment"] ?? "development",
                    level = "error",
                    platform = ".NET",
                    language = "csharp",
                    body = new { message = new { body = exception.ToString() } },
                    request = new { url = context.Request.Path.ToString(), method = context.Request.Method }
                }
            });
            using var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception reportingException)
        {
            logger.LogWarning(reportingException, "Error reporting failed");
        }
    }
}

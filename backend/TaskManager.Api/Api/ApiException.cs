namespace TaskManager.Api.Api;

public sealed class ApiException(int statusCode, object body) : Exception
{
    public int StatusCode { get; } = statusCode;
    public object Body { get; } = body;

    public static ApiException Invalid(string field, string message) =>
        new(400, new Dictionary<string, string[]> { [field] = [message] });

    public static ApiException NotFound() => new(404, new { detail = "Not found." });
}

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using TaskManager.Api.Api;

namespace TaskManager.Api.Ui;

public enum SaveResult
{
    Ok,
    Forbidden,
    Failed
}

public sealed class TaskApiClient(IHttpClientFactory factory, UserSession session)
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    private readonly HttpClient http = factory.CreateClient("ui-api");

    public async Task<IReadOnlyList<TaskResponse>?> GetTasksAsync()
    {
        using var response = await SendAsync(() => new HttpRequestMessage(HttpMethod.Get, "api/tasks/"));
        if (response is null || !response.IsSuccessStatusCode)
        {
            return null;
        }
        return await response.Content.ReadFromJsonAsync<List<TaskResponse>>(Json);
    }

    public async Task<SaveResult> SetConditionAsync(long id, string condition)
    {
        using var response = await SendAsync(() => new HttpRequestMessage(HttpMethod.Patch, $"api/tasks/{id}/")
        {
            Content = JsonContent.Create(new { condition }, options: Json)
        });
        if (response is null)
        {
            return SaveResult.Failed;
        }
        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            return SaveResult.Forbidden;
        }
        return response.IsSuccessStatusCode ? SaveResult.Ok : SaveResult.Failed;
    }

    private async Task<HttpResponseMessage?> SendAsync(Func<HttpRequestMessage> create)
    {
        try
        {
            var response = await SendOnceAsync(create, await session.GetAccessTokenAsync());
            if (response?.StatusCode == HttpStatusCode.Unauthorized)
            {
                response.Dispose();
                if (!await session.ForceRefreshAsync())
                {
                    return null;
                }
                response = await SendOnceAsync(create, await session.GetAccessTokenAsync());
            }
            return response;
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException)
        {
            return null;
        }
    }

    private async Task<HttpResponseMessage?> SendOnceAsync(Func<HttpRequestMessage> create, string? token)
    {
        if (token is null)
        {
            return null;
        }
        using var request = create();
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await http.SendAsync(request);
    }
}

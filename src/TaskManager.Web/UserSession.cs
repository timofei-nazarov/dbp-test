using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.JSInterop;

namespace TaskManager.Web;

public sealed class UserSession(IHttpClientFactory factory, ProtectedSessionStorage storage, TimeProvider clock)
{
    private const string RefreshKey = "refresh_token";
    private static readonly TimeSpan RenewWindow = TimeSpan.FromSeconds(30);

    private readonly HttpClient http = factory.CreateClient("ui-api");
    private readonly SemaphoreSlim gate = new(1, 1);
    private string? access;
    private string? refresh;
    private DateTimeOffset accessExpires;

    public bool IsSignedIn => access is not null;

    public bool IsStaff { get; private set; }

    public async Task<bool> RestoreAsync()
    {
        await gate.WaitAsync();
        try
        {
            if (IsSignedIn)
            {
                return true;
            }
            var stored = await storage.GetAsync<string>(RefreshKey);
            if (!stored.Success || string.IsNullOrEmpty(stored.Value))
            {
                return false;
            }
            refresh = stored.Value;
            return await RefreshAsync();
        }
        catch (Exception exception) when (exception is InvalidOperationException or JSException or System.Security.Cryptography.CryptographicException)
        {
            Clear();
            return false;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task SignOutAsync()
    {
        await gate.WaitAsync();
        try
        {
            Clear();
            await storage.DeleteAsync(RefreshKey);
        }
        catch (Exception exception) when (exception is InvalidOperationException or JSException or System.Security.Cryptography.CryptographicException)
        {
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<bool> SignInAsync(string username, string password)
    {
        using var response = await http.PostAsJsonAsync("api/token/", new { username, password });
        if (!response.IsSuccessStatusCode)
        {
            return false;
        }
        var tokens = await response.Content.ReadFromJsonAsync<TokenPair>();
        if (tokens is null || string.IsNullOrEmpty(tokens.Access) || string.IsNullOrEmpty(tokens.Refresh))
        {
            return false;
        }
        refresh = tokens.Refresh;
        Apply(tokens.Access);
        await storage.SetAsync(RefreshKey, tokens.Refresh);
        return true;
    }

    public async Task<string?> GetAccessTokenAsync()
    {
        if (access is not null && accessExpires - clock.GetUtcNow() > RenewWindow)
        {
            return access;
        }
        await ForceRefreshAsync();
        return access;
    }

    public async Task<bool> ForceRefreshAsync()
    {
        await gate.WaitAsync();
        try
        {
            return await RefreshAsync();
        }
        finally
        {
            gate.Release();
        }
    }

    public void Clear()
    {
        access = null;
        refresh = null;
        IsStaff = false;
        accessExpires = default;
    }

    private async Task<bool> RefreshAsync()
    {
        if (string.IsNullOrEmpty(refresh))
        {
            Clear();
            return false;
        }
        using var response = await http.PostAsJsonAsync("api/token/refresh/", new { refresh });
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            Clear();
            return false;
        }
        if (!response.IsSuccessStatusCode)
        {
            return false;
        }
        var tokens = await response.Content.ReadFromJsonAsync<TokenPair>();
        if (tokens is null || string.IsNullOrEmpty(tokens.Access))
        {
            return false;
        }
        Apply(tokens.Access);
        return true;
    }

    private void Apply(string token)
    {
        var parsed = new JwtSecurityTokenHandler().ReadJwtToken(token);
        access = token;
        accessExpires = new DateTimeOffset(DateTime.SpecifyKind(parsed.ValidTo, DateTimeKind.Utc));
        IsStaff = parsed.Claims.Any(claim => claim.Type == "is_staff" && claim.Value == "true");
    }

    private sealed record TokenPair(
        [property: JsonPropertyName("access")] string? Access,
        [property: JsonPropertyName("refresh")] string? Refresh);
}

using CheckinApi.Config;
using CheckinApi.Extensions;
using CheckinApi.Interfaces;
using CheckinApi.Models;

namespace CheckinApi.Services;

public class StravaAuthService : IAuthService
{
    private const string BaseApiUrl = "https://www.strava.com/oauth/token";

    private readonly CheckinSecrets _secrets;
    private readonly HttpClient _httpClient;
    private readonly ILogger<StravaAuthService> _logger;
    
    public StravaAuthService(CheckinSecrets secrets, HttpClient httpClient, ILogger<StravaAuthService> logger)
    {
        _secrets = secrets;
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task RefreshTokenAsync()
    {
        var url = $"{BaseApiUrl}?client_id={_secrets.Strava.client_id}" +
                  $"&client_secret={_secrets.Strava.client_secret}" +
                  $"&refresh_token={_secrets.Strava.auth.refresh_token}" +
                  $"&grant_type=refresh_token";
        
        HttpResponseMessage? res = null;
        try 
        {
            res = await _httpClient.PostAsJsonAsync(url, string.Empty);

            if (!res.IsSuccessStatusCode) 
            {
                _logger.LogError("Unsuccessful Strava token refresh: {@res}", res.Content.ReadAsStringAsync().Result);
                return;
            }
        
            var json = await res.Content.ReadAsStringAsync();
            var refreshedAuth = json.Deserialize<StravaAuthInfo>();
            
            _secrets.Strava.UpdateAuth(refreshedAuth);
            await File.WriteAllTextAsync(Constants.SecretsFile, _secrets.Serialize());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing Strava config {requestUrl} {@res}", url, res);
        }
    }
    
    public bool IsTokenExpired() => _secrets.Strava.auth.expires_at < DateTimeOffset.UtcNow.ToUnixTimeSeconds(); 
}

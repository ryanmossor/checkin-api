using CheckinApi.Config;
using CheckinApi.Extensions;
using CheckinApi.Interfaces;

namespace CheckinApi.Services;

public class StravaAuthService : IStravaAuthService
{
    private const string BaseApiUrl = "https://www.strava.com/oauth/token";

    private readonly CheckinSecrets _secrets;
    private readonly HttpClient _httpClient;
    private readonly ILogger<StravaAuthService> _logger;
    private readonly CheckinConfig _config;

    public StravaAuthInfo Auth
    {
        get
        {
            if (IsTokenExpired())
                RefreshTokenAsync().GetAwaiter().GetResult();
            return _secrets.Strava.auth;
        }
    }

    public StravaAuthService(
        CheckinSecrets secrets,
        HttpClient httpClient,
        ILogger<StravaAuthService> logger,
        CheckinConfig config)
    {
        _secrets = secrets;
        _httpClient = httpClient;
        _logger = logger;
        _config = config;
    }

    public async Task RefreshTokenAsync()
    {
        var url = $"{BaseApiUrl}?client_id={_secrets.Strava.client_id}" +
                  $"&client_secret={_secrets.Strava.client_secret}" +
                  $"&refresh_token={_secrets.Strava.auth.refresh_token}" +
                  $"&grant_type=refresh_token";

        HttpResponseMessage? response = null;
        try
        {
            _logger.LogDebug("Refreshing Strava token...");
            response = await _httpClient.PostAsJsonAsync(url, string.Empty);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Unsuccessful Strava token refresh: {@res}", response.Content.ReadAsStringAsync().Result);
                return;
            }

            var json = await response.Content.ReadAsStringAsync();
            var refreshedAuth = json.Deserialize<StravaAuthInfo>();

            _secrets.Strava.UpdateAuth(refreshedAuth);
            await File.WriteAllTextAsync(_config.SecretsFile, _secrets.SerializePretty());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing Strava config {requestUrl} {@res}", url, response);
        }
    }

    public bool IsTokenExpired() => _secrets.Strava.auth.expires_at < DateTimeOffset.UtcNow.ToUnixTimeSeconds();
}

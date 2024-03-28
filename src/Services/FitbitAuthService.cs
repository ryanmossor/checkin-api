using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Text;
using CheckinApi.Config;
using CheckinApi.Extensions;
using CheckinApi.Interfaces;

namespace CheckinApi.Services;

public class FitbitAuthService : IFitbitAuthService
{
    private const string BaseApiUrl = "https://api.fitbit.com/oauth2/token";

    private readonly CheckinSecrets _secrets;
    private readonly HttpClient _httpClient;
    private readonly ILogger<FitbitAuthService> _logger;
    private readonly CheckinConfig _config;

    public FitbitAuthService(CheckinSecrets secrets, HttpClient httpClient, ILogger<FitbitAuthService> logger, CheckinConfig config)
    {
        _secrets = secrets;
        _httpClient = httpClient;
        _logger = logger;
        _config = config;
    }

    public FitbitAuthInfo Auth
    {
        get
        {
            if (IsTokenExpired())
                RefreshTokenAsync().GetAwaiter().GetResult();
            return _secrets.Fitbit.auth;
        }
    }

    public async Task RefreshTokenAsync()
    {
        var basicTokenAsBytes = Encoding.UTF8.GetBytes($"{_secrets.Fitbit.client_id}:{_secrets.Fitbit.client_secret}");
        var basicTokenBase64 = Convert.ToBase64String(basicTokenAsBytes);

        var request = new HttpRequestMessage(HttpMethod.Post, BaseApiUrl);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/x-www-form-urlencoded"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basicTokenBase64);

        request.Content = new StringContent(
            content: $"grant_type=refresh_token&refresh_token={_secrets.Fitbit.auth.refresh_token}",
            encoding: Encoding.UTF8,
            mediaType: "application/x-www-form-urlencoded");

        HttpResponseMessage? response = null;
        try
        {
            _logger.LogDebug("Refreshing Fitbit token...");
            response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Unsuccessful Fitbit token refresh: {@res}", response.Content.ReadAsStringAsync().Result);
                return;
            }

            var json = await response.Content.ReadAsStringAsync();
            var refreshedAuth = json.Deserialize<FitbitAuthInfo>();

            _secrets.Fitbit.UpdateAuth(refreshedAuth);
            await File.WriteAllTextAsync(_config.SecretsFile, _secrets.SerializePretty());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing Fitbit config {@res}", response);
        }
    }

    public bool IsTokenExpired()
    {
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(_secrets.Fitbit.auth.access_token);
        return ((DateTimeOffset)jwt.ValidTo).ToUnixTimeSeconds() < DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }
}

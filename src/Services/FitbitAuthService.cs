using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Text;
using CheckinApi.Config;
using CheckinApi.Extensions;
using CheckinApi.Interfaces;
using CheckinApi.Models;

namespace CheckinApi.Services;

public class FitbitAuthService : IAuthService
{
    private const string BaseApiUrl = "https://api.fitbit.com/oauth2/token";

    private readonly CheckinSecrets _secrets;
    private readonly HttpClient _httpClient;
    private readonly ILogger<FitbitAuthService> _logger;
    
    public FitbitAuthService(CheckinSecrets secrets, HttpClient httpClient, ILogger<FitbitAuthService> logger)
    {
        _secrets = secrets;
        _httpClient = httpClient;
        _logger = logger;
    }
    
    public async Task RefreshTokenAsync()
    {
        var basicTokenAsBytes = Encoding.UTF8.GetBytes($"{_secrets.Fitbit.client_id}:{_secrets.Fitbit.client_secret}");
        var basicTokenBase64 = Convert.ToBase64String(basicTokenAsBytes);
        
        var request = new HttpRequestMessage(HttpMethod.Post, BaseApiUrl);
        _httpClient.DefaultRequestHeaders.Accept.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/x-www-form-urlencoded"));
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basicTokenBase64);
        
        request.Content = new StringContent(
            content: $"grant_type=refresh_token&refresh_token={_secrets.Fitbit.auth.refresh_token}",
            encoding: Encoding.UTF8,
            mediaType: "application/x-www-form-urlencoded"); 
            
        HttpResponseMessage? res = null;
        try 
        {
            res = await _httpClient.SendAsync(request);

            if (!res.IsSuccessStatusCode) 
            {
                _logger.LogError("Unsuccessful Fitbit token refresh: {@res}", res.Content.ReadAsStringAsync().Result);
                return;
            }
        
            var json = await res.Content.ReadAsStringAsync();
            var refreshedAuth = json.Deserialize<FitbitAuthInfo>();
            
            _secrets.Fitbit.UpdateAuth(refreshedAuth);
            await File.WriteAllTextAsync(Constants.SecretsFile, _secrets.Serialize());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing Strava config {@res}", res);
        }
    }

    public bool IsTokenExpired()
    {
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(_secrets.Fitbit.auth.access_token);
        return ((DateTimeOffset)jwt.ValidTo).ToUnixTimeSeconds() < DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }
}

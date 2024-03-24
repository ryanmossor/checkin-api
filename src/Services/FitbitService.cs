using System.Net.Http.Headers;
using System.Net.Mime;
using CheckinApi.Config;
using CheckinApi.Extensions;
using CheckinApi.Interfaces;
using CheckinApi.Models;

namespace CheckinApi.Services;

public class FitbitService : IHealthTrackingService
{
    private const string BaseApiUrl = "https://api.fitbit.com";

    private readonly HttpClient _httpClient;
    private readonly CheckinSecrets _secrets;
    private readonly ITokenService _tokenService;
    private readonly ILogger<FitbitService> _logger;

    public FitbitService(HttpClient httpClient, CheckinSecrets secrets, ITokenService tokenService, ILogger<FitbitService> logger)
    {
        _httpClient = httpClient;
        _secrets = secrets;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task<WeightData?> GetWeightDataAsync(string date)
    {
        using (_logger.BeginScope("Getting weight data for {date}", date))
        {
            if (_tokenService.IsTokenExpired())
                await _tokenService.RefreshTokenAsync();
            
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(MediaTypeNames.Application.Json));
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _secrets.Fitbit.auth.access_token);

            var url = $"{BaseApiUrl}/1/user/-/body/log/weight/date/{date}.json";
            try
            {
                var json = await _httpClient.GetStringAsync(url);
                var data = json.Deserialize<WeightData>();
                _logger.LogDebug("Retrieved weight data: {@data}", data);
                
                return data;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving weight data: {requestUrl}", url);
                return null;
            }
        }
    }
}
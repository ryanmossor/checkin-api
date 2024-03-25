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
    private readonly IAuthService _authService;
    private readonly ILogger<FitbitService> _logger;

    public FitbitService(HttpClient httpClient, CheckinSecrets secrets, IAuthService authService, ILogger<FitbitService> logger)
    {
        _httpClient = httpClient;
        _secrets = secrets;
        _authService = authService;
        _logger = logger;
    }

    public async Task<Weight[]?> GetWeightDataAsync(List<CheckinItem> queue)
    {
        if (_authService.IsTokenExpired())
            await _authService.RefreshTokenAsync();
        
        var startDate = queue.First().CheckinFields.Date;
        var endDate = queue.Last().CheckinFields.Date;
        
        using (_logger.BeginScope("Getting weight data from {start} to {end}", startDate, endDate)) 
        {
            if (_authService.IsTokenExpired())
                await _authService.RefreshTokenAsync();
            
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(MediaTypeNames.Application.Json));
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _secrets.Fitbit.auth.access_token);

            var url = $"{BaseApiUrl}/1/user/-/body/log/weight/date/{startDate}/{endDate}.json";
            try
            {
                var json = await _httpClient.GetStringAsync(url);
                var data = json.Deserialize<WeightData>();
                _logger.LogDebug("Retrieved weight data: {@data}", data);
                
                return data.Weight;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving weight data: {requestUrl}", url);
                return null;
            }
        }
    }
}

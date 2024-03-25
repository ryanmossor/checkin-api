using System.Net.Http.Headers;
using System.Net.Mime;
using CheckinApi.Config;
using CheckinApi.Extensions;
using CheckinApi.Interfaces;
using CheckinApi.Models;

namespace CheckinApi.Services;

public class StravaService : IActivityService
{
    private const string BaseApiUrl = "https://www.strava.com/api/v3";

    private readonly HttpClient _httpClient;
    private readonly CheckinSecrets _secrets;
    private readonly IAuthService _authService;
    private readonly ILogger<StravaService> _logger;

    public StravaService(HttpClient httpClient, CheckinSecrets secrets, IAuthService authService, ILogger<StravaService> logger)
    {
        _httpClient = httpClient;
        _secrets = secrets;
        _authService = authService;
        _logger = logger;
    }

    public async Task<StravaActivity[]?> GetActivityDataAsync(List<CheckinItem> queue)
    {
        if (_authService.IsTokenExpired())
            await _authService.RefreshTokenAsync();
        
        var firstQueueItemDate = DateTimeOffset.Parse(queue.First().CheckinFields.Date);
        var lastQueueItemDate = DateTimeOffset.Parse(queue.Last().CheckinFields.Date).AddDays(1).AddSeconds(-1);
        
        using (_logger.BeginScope("Getting Strava activity data from {start} to {end}", 
            firstQueueItemDate.ToString("yyyy/MM/dd hh:mm:ss"),
            lastQueueItemDate.ToString("yyyy/MM/dd hh:mm:ss"))) 
        {
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(MediaTypeNames.Application.Json));
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _secrets.Strava.auth.access_token); 
            
            var before = lastQueueItemDate.ToUnixTimeSeconds();
            var after = firstQueueItemDate.ToUnixTimeSeconds();
            var url = $"{BaseApiUrl}/athlete/activities?before={before}&after={after}";
            
            try 
            {
                var json = await _httpClient.GetStringAsync(url);
                var data = json.Deserialize<StravaActivity[]>();
                
                _logger.LogDebug("Retrieved Strava activities: {@data}", data.ToList());
                return data;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving Strava activities: {requestUrl}", url);
                return null;
            }
        }
    }
}

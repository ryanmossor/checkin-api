using System.Net.Http.Headers;
using System.Net.Mime;
using CheckinApi.Extensions;
using CheckinApi.Models;
using CheckinApi.Models.Strava;

namespace CheckinApi.Services;

public class StravaService : IActivityService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<StravaService> _logger;

    private const string BaseApiUrl = "https://www.strava.com/api/v3/athlete/activities";

    public StravaService(HttpClient httpClient, ILogger<StravaService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<StravaActivity[]?> GetActivityData(List<CheckinItem> queue)
    {
        var firstQueueItemDate = DateTimeOffset.Parse(queue.First().CheckinFields.Date);
        var lastQueueItemDate = DateTimeOffset.Parse(queue.Last().CheckinFields.Date).AddDays(1).AddSeconds(-1);
        
        _httpClient.DefaultRequestHeaders.Accept.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(MediaTypeNames.Application.Json));
        // TODO: handle secrets
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "redacted");

        var before = lastQueueItemDate.ToUnixTimeSeconds();
        var after = firstQueueItemDate.ToUnixTimeSeconds();
        var url = $"{BaseApiUrl}?before={before}&after={after}";
        
        try 
        {
            var json = await _httpClient.GetStringAsync(url);
            var data = json.Deserialize<StravaActivity[]>();
            
            _logger.LogDebug(
                "Retrieved Strava activities from {start} to {end}: {@data}", 
                firstQueueItemDate.ToString("yyyy/MM/dd hh:mm:ss"),
                lastQueueItemDate.ToString("yyyy/MM/dd hh:mm:ss"),
                data);
            
            return data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error retrieving Strava activities from {start} to {end}: {requestUrl}",
                firstQueueItemDate.ToString("yyyy/MM/dd hh:mm:ss"),
                lastQueueItemDate.ToString("yyyy/MM/dd hh:mm:ss"),
                url);
            
            return null;
        }
    }
}

public interface IActivityService
{
    Task<StravaActivity[]?> GetActivityData(List<CheckinItem> queue);
}
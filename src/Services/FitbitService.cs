using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text;
using CheckinApi.Extensions;
using CheckinApi.Models;

namespace CheckinApi.Services;

public class FitbitService : IHealthTrackingService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FitbitService> _logger;

    private const string BaseApiUrl = "https://api.fitbit.com/1/user/-/body/log/weight/date";

    public FitbitService(HttpClient httpClient, ILogger<FitbitService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<WeightData?> GetWeightData(string date)
    {
        _logger.LogInformation("Getting weight data...");
        
        _httpClient.DefaultRequestHeaders.Accept.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(MediaTypeNames.Application.Json));
        // TODO: handle secrets
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "redacted");

        var url = $"{BaseApiUrl}/{date}.json";
        try
        {
            var json = await _httpClient.GetStringAsync(url);
            var data = json.Deserialize<WeightData>();
            _logger.LogDebug("Retrieved Fitbit data for {date}: {@data}", date, data);
            
            return data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving Fitbit data for {date}: {requestUrl}", date, url);
            return null;
        }
    }
}

public interface IHealthTrackingService
{
    Task<WeightData?> GetWeightData(string date);
}

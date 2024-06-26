using System.Net.Http.Headers;
using System.Net.Mime;
using CheckinApi.Extensions;
using CheckinApi.Interfaces;
using CheckinApi.Models;

namespace CheckinApi.Services;

public class FitbitService : IHealthTrackingService
{
    private const string BaseApiUrl = "https://api.fitbit.com";

    private readonly HttpClient _httpClient;
    private readonly IFitbitAuthService _authService;
    private readonly ILogger<FitbitService> _logger;

    public FitbitService(HttpClient httpClient, IFitbitAuthService authService, ILogger<FitbitService> logger)
    {
        _httpClient = httpClient;
        _authService = authService;
        _logger = logger;
    }

    public async Task<List<Weight>> GetWeightDataAsync(List<CheckinItem> queue)
    {
        var startDate = queue.First().CheckinFields.Date;
        var endDate = queue.Last().CheckinFields.Date;

        using var scope = _logger.BeginScope("Getting weight data from {start} to {end}", startDate, endDate);

        var url = $"{BaseApiUrl}/1/user/-/body/log/weight/date/{startDate}/{endDate}.json";

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(MediaTypeNames.Application.Json));
        request.Headers.Authorization = new AuthenticationHeaderValue(
            _authService.Auth.token_type,
            _authService.Auth.access_token);

        HttpResponseMessage? response = null;
        try
        {
            response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Unsuccessful Fitbit API call: {@res}", response.Content.ReadAsStringAsync().Result);
                return new List<Weight>();
            }

            var content = await response.Content.ReadAsStringAsync();
            var data = content.Deserialize<WeightData>();
            _logger.LogDebug("Retrieved weight data: {@data}", data);

            return data.Weight;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving weight data: {requestUrl} {@res}", url, response);
            return new List<Weight>();
        }
    }
}

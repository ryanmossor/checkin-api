using System.Diagnostics;
using CheckinApi.Extensions;
using CheckinApi.Models;
using CheckinApi.Models.Strava;

namespace CheckinApi.Services;

public class CheckinQueueProcessor : ICheckinQueueProcessor
{
    private readonly ILogger<CheckinQueueProcessor> _logger;
    private readonly ICheckinLists _lists;
    private readonly IActivityService _activityService;
    private readonly IHealthTrackingService _healthTrackingService;

    public CheckinQueueProcessor(
        ICheckinLists lists,
        IActivityService activityService,
        IHealthTrackingService healthTrackingService,
        ILogger<CheckinQueueProcessor> logger)
    {
        _lists = lists;
        _activityService = activityService;
        _healthTrackingService = healthTrackingService;
        _logger = logger;
    }

    public async Task<CheckinResponse> Process(List<CheckinItem> queue)
    {
        var stopwatch = Stopwatch.StartNew();
        var unprocessed = new List<CheckinItem>();
        var results = new List<CheckinResult>();

        var existingFiles = Directory.GetFiles("./data/results").Select(Path.GetFileNameWithoutExtension).ToList();

        StravaActivity[]? activityData = null;
        if (queue.Any(queueItem => queueItem.FormResponse.Keys.Any(key => _lists.TrackedActivities.Contains(key))))
        {
            activityData = await _activityService.GetActivityData(queue);
        }

        foreach (var item in queue)
        {
            using (_logger.BeginScope("Processing check-in item for {date}", item.CheckinFields.Date))
            {
                if (!item.FormResponse.TryGetValue("Feel Well-Rested", out _))
                {
                    _logger.LogInformation("Morning check-in not completed. Skipping {@item}", item);
                    unprocessed.Add(item);
                    continue;
                }

                if (item.SleepStart.HasValue && item.SleepEnd.HasValue)
                {
                    var totalTime = TimeSpan.FromSeconds(item.SleepEnd.Value - item.SleepStart.Value).ToString(@"h\:mm");
                    item.FormResponse["Total Time in Bed"] = totalTime;
                }
                else
                {
                    _logger.LogWarning(
                        "Missing sleep start and/or end time: {sleepStart}, {sleepEnd}", item.SleepStart, item.SleepEnd); 
                }

                if (item.GetWeight != null)
                {
                    var weightData = await _healthTrackingService.GetWeightData(item.CheckinFields.Date);
                    if (weightData.Weight.Any())
                    {
                        item.FormResponse["BMI"] = weightData.Weight[0].Bmi.ToString();
                        item.FormResponse["Body fat %"] = weightData.Weight[0].Fat.ToString();
                        item.FormResponse["Weight (lbs)"] = weightData.Weight[0].Lbs.ToString();
                    }
                }

                if (activityData != null && item.FormResponse.Keys.Any(key => _lists.TrackedActivities.Contains(key)))
                {
                    var date = DateTime.Parse(item.CheckinFields.Date);

                    foreach (var activity in _lists.TrackedActivities)
                    {
                        if (!activityData.Any(a => a.Type == activity && DateTime.Parse(a.StartDateLocal).Date == date.Date))
                            continue;
                        
                        var activitySums = activityData
                            .Where(a => a.Type == activity && DateTime.Parse(a.StartDateLocal).Date == date.Date)
                            .Sum(a => a.Distance);
                        
                        _logger.LogDebug("Sum for {activity}: {sum}", activity, activitySums);
                        item.FormResponse[activity] = activitySums.ToString();
                    }
                }

                try
                {
                    var json = item.Serialize().Replace("\\u003C", "<");
                    if (existingFiles.Contains(item.CheckinFields.Date))
                    {
                        _logger.LogInformation("Overwriting existing results file");
                    }

                    await File.WriteAllTextAsync($"./data/results/{item.CheckinFields.Date}.json", json);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error writing check-in results to file");
                }

                var resultsArr = _lists.FullChecklist.Select(x => item.FormResponse.GetValueOrDefault(x));
                var resultsString = string.Join(",", resultsArr);

                results.Add(new CheckinResult(item.CheckinFields, resultsString));
                _logger.LogDebug("Results string: {resultsString}", resultsString);
            }
        }

        _logger.LogInformation(
            "Processed {processedCount}, skipped {skippedCount} item(s) in {elapsed} ms: {@skippedItems}",
            results.Count,
            unprocessed.Count,
            stopwatch.ElapsedMilliseconds,
            unprocessed);
        return new CheckinResponse(unprocessed, results);
    }
}

public interface ICheckinQueueProcessor
{
    Task<CheckinResponse> Process(List<CheckinItem> queue);
}
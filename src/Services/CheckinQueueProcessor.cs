using System.Diagnostics;
using System.Text;
using CheckinApi.Extensions;
using CheckinApi.Interfaces;
using CheckinApi.Models;

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

    public async Task<CheckinResponse> ProcessAsync(List<CheckinItem> queue)
    {
        var stopwatch = Stopwatch.StartNew();
        var unprocessed = new List<CheckinItem>();
        var results = new List<CheckinResult>();

        StravaActivity[]? activityData = null;
        if (queue.Any(queueItem => queueItem.FormResponse.Keys.Any(key => _lists.TrackedActivities.Contains(key))))
        {
            var res = await _activityService.GetActivityDataAsync(queue);
            activityData = res;
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

                var updatedItem = UpdateTimeInBed(item);

                if (updatedItem.GetWeight != null)
                    updatedItem = await UpdateWeightDataAsync(updatedItem);

                (updatedItem, var skipCurrentItem) = ProcessActivityData(updatedItem, activityData);
                if (skipCurrentItem)
                {
                    _logger.LogError(
                        "Error retrieving activity data with tracked activities in form response. Skipping {@item}",
                        updatedItem);
                
                    unprocessed.Add(updatedItem);
                    continue;
                }

                try
                {
                    var json = updatedItem.SerializeFlat().Replace("\\u003C", "<");
                    await File.WriteAllTextAsync(
                        Path.Combine(Constants.ResultsDir, $"{updatedItem.CheckinFields.Date}.json"),
                        json);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error writing check-in results to file");
                }

                var resultsString = string.Join(",", _lists.FullChecklist.Select(x => updatedItem.FormResponse.GetValueOrDefault(x)));
                results.Add(new CheckinResult(updatedItem.CheckinFields, resultsString));
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

    private (CheckinItem, bool skipCurrentItem) ProcessActivityData(CheckinItem item, StravaActivity[]? activityData)
    {
        var itemContainsTrackedActivities = item.FormResponse.Keys.Any(key => _lists.TrackedActivities.Contains(key));
        
        if (!itemContainsTrackedActivities) 
            return (item, skipCurrentItem: false);
        
        if (activityData == null)
            return (item, skipCurrentItem: true);

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

        return (item, skipCurrentItem: false);
    }

    private async Task<CheckinItem> UpdateWeightDataAsync(CheckinItem item)
    {
        var weightData = await _healthTrackingService.GetWeightDataAsync(item.CheckinFields.Date);
        if (weightData == null || !weightData.Weight.Any()) 
            return item;
        
        item.FormResponse["BMI"] = weightData.Weight[0].Bmi.ToString();
        item.FormResponse["Body fat %"] = weightData.Weight[0].Fat.ToString();
        item.FormResponse["Weight (lbs)"] = weightData.Weight[0].Lbs.ToString();

        return item;
    }

    private CheckinItem UpdateTimeInBed(CheckinItem item)
    {
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

        return item;
    }
}

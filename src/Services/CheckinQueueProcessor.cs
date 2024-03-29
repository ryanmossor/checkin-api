using System.Diagnostics;
using CheckinApi.Config;
using CheckinApi.Extensions;
using CheckinApi.Interfaces;
using CheckinApi.Models;

namespace CheckinApi.Services;

public class CheckinQueueProcessor : ICheckinQueueProcessor
{
    private readonly ICheckinLists _lists;
    private readonly IActivityService _activityService;
    private readonly IHealthTrackingService _healthTrackingService;
    private readonly ILogger<CheckinQueueProcessor> _logger;
    private readonly CheckinConfig _config;

    public CheckinQueueProcessor(
        ICheckinLists lists,
        IActivityService activityService,
        IHealthTrackingService healthTrackingService,
        ILogger<CheckinQueueProcessor> logger,
        CheckinConfig config)
    {
        _lists = lists;
        _activityService = activityService;
        _healthTrackingService = healthTrackingService;
        _logger = logger;
        _config = config;
    }

    public async Task<CheckinResponse> ProcessSavedResultsAsync(string dates, bool concatResults, string? delimiter)
    {
        using (_logger.BeginScope("Processing check-in items for {@dates}", dates))
        {
            var files = Directory.GetFiles(_config.ResultsDir).Select(Path.GetFileNameWithoutExtension);
            var missingResults = dates.Split(',').Where(f => !files.Contains(f)).ToList();

            if (missingResults.Any())
            {
                _logger.LogError("Error retrieving data for {@missingResults}", missingResults);
            }

            var validDates = dates.Split(',').Order().Where(d => !missingResults.Contains(d));
            _logger.LogDebug("Processing valid dates: {@validDates}", validDates);

            var results = new List<CheckinResult>();
            foreach (var date in validDates)
            {
                try
                {
                    var contents = await File.ReadAllTextAsync(Path.Combine(_config.ResultsDir, $"{date}.json"));
                    var item = contents.Deserialize<CheckinItem>();

                    var resultsString = item.BuildResultsString(_lists.FullChecklist);
                    results.Add(new CheckinResult(item.CheckinFields, resultsString));
                    _logger.LogInformation("Results string for {date}: {resultsString}", resultsString, item.CheckinFields.Date);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error retrieving data for {date}", date);
                }
            }

            if (concatResults)
            {
                var concatenatedResults = results.ConcatenateResults(delimiter);
                _logger.LogInformation("Concatenated results: {@results}", concatenatedResults);
                return new CheckinResponse(concatenatedResults);
            }

            return new CheckinResponse(results);
        }
    }

    public async Task<CheckinResponse> ProcessQueueAsync(List<CheckinItem> queue, bool concatResults, bool forceProcessing, string? delimiter)
    {
        var stopwatch = Stopwatch.StartNew();
        var unprocessed = new List<CheckinItem>();
        var results = new List<CheckinResult>();

        var weightData = new List<Weight>();
        if (queue.Any(queueItem => queueItem.GetWeight))
        {
            weightData = await _healthTrackingService.GetWeightDataAsync(queue);
        }

        var activityData = new List<StravaActivity>();
        if (queue.Any(queueItem => queueItem.FormResponse.Keys.Any(key => _lists.TrackedActivities.Contains(key))))
        {
            activityData = await _activityService.GetActivityDataAsync(queue);
        }

        foreach (var item in queue.OrderBy(x => x.CheckinFields.Date).ToList())
        {
            using (_logger.BeginScope("Processing check-in item for {date}", item.CheckinFields.Date))
            {
                if (!forceProcessing && ShouldSkipItem(item, weightData, activityData))
                {
                    unprocessed.Add(item);
                    continue;
                }

                item.UpdateTimeInBed();
                item.UpdateWeightData(weightData);
                item.ProcessActivityData(activityData, _lists.TrackedActivities);

                try
                {
                    var json = item.Serialize();
                    await File.WriteAllTextAsync(
                        Path.Combine(_config.ResultsDir, $"{item.CheckinFields.Date}.json"),
                        json);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error writing check-in results to file");
                }

                if (item.FormResponse.Keys.Any(x => !_lists.FullChecklist.Contains(x)))
                {
                    _logger.LogInformation(
                        "Items in queue that aren't in full checklist: {@items}",
                        item.FormResponse.Keys.Where(x => !_lists.FullChecklist.Contains(x)));
                }

                var resultsString = item.BuildResultsString(_lists.FullChecklist);
                results.Add(new CheckinResult(item.CheckinFields, resultsString));
                _logger.LogInformation("Results string: {resultsString}", resultsString);
            }
        }

        _logger.LogInformation(
            "Processed {processedCount}, skipped {skippedCount} item(s) in {elapsed} ms: {@skippedItems}",
            results.Count,
            unprocessed.Count,
            stopwatch.ElapsedMilliseconds,
            unprocessed);

        if (concatResults)
        {
            var concatenatedResults = results.ConcatenateResults(delimiter);
            _logger.LogInformation("Concatenated results: {@results}", concatenatedResults);
            return new CheckinResponse(concatenatedResults, unprocessed);
        }

        return new CheckinResponse(results, unprocessed);
    }

    private bool ShouldSkipItem(CheckinItem item, List<Weight> weightData, List<StravaActivity> activityData)
    {
        if (!item.FormResponse.TryGetValue("Feel Well-Rested", out _))
        {
            _logger.LogInformation("Morning check-in not completed. Skipping...");
            return true;
        }

        var matchingWeightData = weightData.Any(w => w.Date == item.CheckinFields.Date);
        if (item.GetWeight && !matchingWeightData)
        {
            _logger.LogWarning("getWeight flag set but no matching weight data found. Skipping...");
            return true;
        }

        var itemContainsTrackedActivities = item.FormResponse.Keys.Any(key => _lists.TrackedActivities.Contains(key));
        var matchingActivityData = activityData.Any(a => a.Date == item.CheckinFields.Date);
        if (itemContainsTrackedActivities && !matchingActivityData)
        {
            _logger.LogWarning(
                "Tracked activities in queue but no activity data found for {@missingActivities}. Skipping...",
                item.FormResponse.Keys.Where(key => _lists.TrackedActivities.Contains(key)).ToList());
            return true;
        }

        return false;
    }
}

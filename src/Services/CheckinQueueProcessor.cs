using System.Diagnostics;
using CheckinApi.Extensions;
using CheckinApi.Interfaces;
using CheckinApi.Models;

namespace CheckinApi.Services;

public class CheckinQueueProcessor : ICheckinQueueProcessor
{
    private readonly IActivityService _activityService;
    private readonly IHealthTrackingService _healthTrackingService;
    private readonly ILogger<CheckinQueueProcessor> _logger;
    private readonly ICheckinRepository _repository;

    public CheckinQueueProcessor(
        IActivityService activityService,
        IHealthTrackingService healthTrackingService,
        ILogger<CheckinQueueProcessor> logger,
        ICheckinRepository repository)
    {
        _activityService = activityService;
        _healthTrackingService = healthTrackingService;
        _logger = logger;
        _repository = repository;
    }

    public async Task<CheckinResponse> ProcessSavedResultsAsync(string dates, bool concatResults, string? delimiter)
    {
        using var scope = _logger.BeginScope("Processing check-in items for {@dates}", dates);

        var existingDates = _repository.GetAllCheckinDates();
        var missingResults = dates.Split(',').Where(f => !existingDates.Contains(f)).ToList();

        if (missingResults.Any())
        {
            _logger.LogError("Error retrieving data for {@missingResults}", missingResults);
        }

        var validDates = dates.Split(',').Order().Where(d => !missingResults.Contains(d)).ToList();
        _logger.LogDebug("Processing valid dates: {@validDates}", validDates);

        var checkinItems = await _repository.GetCheckinItemsAsync(validDates);
        var checkinLists = await _repository.GetCheckinListsAsync();
        var results = new List<CheckinResult>();

        foreach (var item in checkinItems)
        {
            var resultsString = item.BuildResultsString(checkinLists.FullChecklist);
            results.Add(new CheckinResult(item.CheckinFields, resultsString));
            _logger.LogInformation("Results string for {date}: {resultsString}", resultsString, item.CheckinFields.Date);
        }

        if (concatResults)
        {
            var concatenatedResults = results.ConcatenateResults(delimiter);
            _logger.LogInformation("Concatenated results: {@results}", concatenatedResults);
            return new CheckinResponse(concatenatedResults);
        }

        return new CheckinResponse(results);
    }

    public async Task<CheckinResponse> ProcessQueueAsync(
        List<CheckinItem> queue,
        bool concatResults,
        bool forceProcessing,
        string? delimiter)
    {
        var stopwatch = Stopwatch.StartNew();
        var unprocessed = new List<CheckinItem>();
        var results = new List<CheckinResult>();

        var weightData = new List<Weight>();
        if (queue.Any(queueItem => queueItem.GetWeight))
        {
            weightData = await _healthTrackingService.GetWeightDataAsync(queue);
        }

        var lists = await _repository.GetCheckinListsAsync();
        var activityData = new List<StravaActivity>();
        if (queue.Any(queueItem => queueItem.FormResponse.Keys.Any(key => lists.TrackedActivities.Contains(key))))
        {
            activityData = await _activityService.GetActivityDataAsync(queue);
        }

        foreach (var item in queue.OrderBy(x => x.CheckinFields.Date).ToList())
        {
            using var scope = _logger.BeginScope("Processing check-in item for {date}", item.CheckinFields.Date);

            if (!forceProcessing && ShouldSkipItem(item, weightData, activityData, lists.TrackedActivities))
            {
                unprocessed.Add(item);
                continue;
            }

            item.UpdateTimeInBed();
            item.UpdateWeightData(weightData);
            item.ProcessActivityData(activityData, lists.TrackedActivities);

            await _repository.SaveCheckinItemAsync(item);

            if (item.FormResponse.Keys.Any(x => !lists.FullChecklist.Contains(x)))
            {
                _logger.LogInformation(
                    "Items in queue that aren't in full checklist: {@items}",
                    item.FormResponse.Keys.Where(x => !lists.FullChecklist.Contains(x)));
            }

            var resultsString = item.BuildResultsString(lists.FullChecklist);
            results.Add(new CheckinResult(item.CheckinFields, resultsString));
            _logger.LogInformation("Results string: {resultsString}", resultsString);
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

    private bool ShouldSkipItem(
        CheckinItem item,
        List<Weight> weightData,
        List<StravaActivity> activityData,
        List<string> trackedActivities)
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

        var itemContainsTrackedActivities = item.FormResponse.Keys.Any(trackedActivities.Contains);
        var matchingActivityData = activityData.Any(a => a.Date == item.CheckinFields.Date);
        if (itemContainsTrackedActivities && !matchingActivityData)
        {
            _logger.LogWarning(
                "Tracked activities in queue but no activity data found for {@missingActivities}. Skipping...",
                item.FormResponse.Keys.Where(trackedActivities.Contains).ToList());
            return true;
        }

        return false;
    }
}

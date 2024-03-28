using System.Diagnostics;
using System.Text;
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

    public async Task<CheckinResponse> ProcessSavedResultsAsync(string dates, bool concatResults)
    {
        var files = Directory.GetFiles(_config.ResultsDir).Select(Path.GetFileNameWithoutExtension);
        var missingResults = dates.Split(',').Where(f => !files.Contains(f)).ToList();

        if (missingResults.Any())
        {
            _logger.LogError("Error retrieving data for {@missingResults}", missingResults);
        }

        var validDates = dates.Split(',').Order().Where(d => !missingResults.Contains(d));
        _logger.LogDebug("Dates to process: {@dates}", validDates);

        var results = new List<CheckinResult>();
        foreach (var date in validDates)
        {
            try
            {
                var contents = await File.ReadAllTextAsync(Path.Combine(_config.ResultsDir, $"{date}.json"));
                var item = contents.Deserialize<CheckinItem>();

                var resultsString = string.Join(",", _lists.FullChecklist.Select(x => item.FormResponse.GetValueOrDefault(x)));
                results.Add(new CheckinResult(item.CheckinFields, resultsString));
                _logger.LogInformation("Results string for {date}: {resultsString}", resultsString, item.CheckinFields.Date);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving data for {date}", date);
            }
        }

        if (concatResults)
            return new CheckinResponse(ConcatenateResults(results));

        return new CheckinResponse(results);
    }

    public async Task<CheckinResponse> ProcessQueueAsync(List<CheckinItem> queue, bool concatResults, bool forceProcessing)
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

                var updatedItem = UpdateTimeInBed(item);
                updatedItem = UpdateWeightData(updatedItem, weightData);
                updatedItem = ProcessActivityData(updatedItem, activityData);

                try
                {
                    var json = updatedItem.Serialize();
                    await File.WriteAllTextAsync(
                        Path.Combine(_config.ResultsDir, $"{updatedItem.CheckinFields.Date}.json"),
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

                var resultsString = string.Join(",", _lists.FullChecklist.Select(x => updatedItem.FormResponse.GetValueOrDefault(x)));
                results.Add(new CheckinResult(updatedItem.CheckinFields, resultsString));
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
            return new CheckinResponse(ConcatenateResults(results), unprocessed);

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
            _logger.LogWarning("getWeight flag set but no matching weight data found");
            return true;
        }

        var checkinDate = DateTime.Parse(item.CheckinFields.Date).Date;
        var itemContainsTrackedActivities = item.FormResponse.Keys.Any(key => _lists.TrackedActivities.Contains(key));
        var matchingActivityData = activityData.Any(a => DateTime.Parse(a.StartDateLocal).Date == checkinDate);
        if (itemContainsTrackedActivities && !matchingActivityData)
        {
            _logger.LogWarning(
                "Tracked activities in queue but no activity data found for {@missingActivities}",
                item.FormResponse.Keys.Where(key => _lists.TrackedActivities.Contains(key)).ToList());

            return true;
        }

        return false;
    }

    private CheckinItem ProcessActivityData(CheckinItem item, List<StravaActivity> activityData)
    {
        if (activityData.Count == 0)
            return item;

        var date = DateTime.Parse(item.CheckinFields.Date);
        foreach (var activity in _lists.TrackedActivities)
        {
            var matchingActivities = activityData
                .Where(a => a.Type == activity && DateTime.Parse(a.StartDateLocal).Date == date.Date)
                .ToList();

            if (!matchingActivities.Any())
                continue;

            var activitySums = matchingActivities.Sum(a => a.Distance);

            _logger.LogDebug("Sum for {activity}: {sum}", activity, activitySums);
            item.FormResponse[activity] = activitySums.ToString();
        }

        return item;
    }

    private CheckinItem UpdateWeightData(CheckinItem item, List<Weight> weightData)
    {
        if (!item.GetWeight)
            return item;

        var data = weightData.FirstOrDefault(w => w.Date == item.CheckinFields.Date);
        if (data == null)
            return item;

        item.FormResponse["BMI"] = data.Bmi.ToString();
        item.FormResponse["Body fat %"] = data.Fat.ToString();
        item.FormResponse["Weight (lbs)"] = data.Lbs.ToString();

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

    private List<CheckinResult> ConcatenateResults(List<CheckinResult> checkinResults)
    {
        const char columnDelimiter = '|';

        var resultsByMonth = checkinResults.GroupBy(r => r.Month).ToList();

        var concatenatedResults = new List<CheckinResult>();
        foreach (var resultGroup in resultsByMonth)
        {
            var dates = resultGroup.Select(r => DateTime.Parse(r.Date)).ToList();
            var startDate = dates.Min();
            var endDate = dates.Max();

            var sb = new StringBuilder();
            for (var date = startDate; date <= endDate; date = date.AddDays(1))
            {
                if (sb.Length > 0)
                    sb.Append(columnDelimiter);

                sb.Append(date.Day);

                var matchingResult = resultGroup.FirstOrDefault(r => r.Date == date.ToString("yyyy-MM-dd"));
                if (matchingResult != null)
                    sb.Append($",{matchingResult.ResultsString}");
            }

            var firstRes = resultGroup.First();
            var result = new CheckinResult(
                new CheckinFields(firstRes.SpreadsheetName, firstRes.Date, firstRes.Month, firstRes.CellReference),
                resultsString: sb.ToString());

            _logger.LogInformation("Concatenated results for month {month}: {@res}", firstRes.Month, result);
            concatenatedResults.Add(result);
        }

        return concatenatedResults;
    }
}

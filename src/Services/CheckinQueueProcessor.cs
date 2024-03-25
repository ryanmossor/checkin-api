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
    
    public async Task<CheckinResponse> ProcessSavedResultsAsync(string dates) 
    {
        var files = Directory.GetFiles(Constants.ResultsDir).Select(Path.GetFileNameWithoutExtension);
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
                var contents = await File.ReadAllTextAsync(Path.Combine(Constants.ResultsDir, $"{date}.json"));
                var item = contents.Deserialize<CheckinItem>();
                
                var resultsString = string.Join(",", _lists.FullChecklist.Select(x => item.FormResponse.GetValueOrDefault(x)));
                results.Add(new CheckinResult(item.CheckinFields, resultsString));
                _logger.LogDebug("Results string for {date}: {resultsString}", resultsString, item.CheckinFields.Date);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving data for {date}", date);
            }
        }

        return new CheckinResponse(ConcatenateResults(results));
    }

    public async Task<CheckinResponse> ProcessQueueAsync(List<CheckinItem> queue)
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

        foreach (var item in queue.OrderBy(x => x.CheckinFields.Date).ToList())
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

        return new CheckinResponse(ConcatenateResults(results), unprocessed);
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
            
            _logger.LogDebug("Concatenated results for month {month}: {@res}", firstRes.Month, result);
            concatenatedResults.Add(result);
        }

        return concatenatedResults;
    }
}

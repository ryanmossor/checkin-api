using System.Diagnostics;
using CheckinApi.Extensions;
using CheckinApi.Models;

namespace CheckinApi.Services;

public class CheckinQueueProcessor : ICheckinQueueProcessor
{
    private readonly ILogger<CheckinQueueProcessor> _logger;
    private readonly ICheckinLists _lists;
    
    public CheckinQueueProcessor(ILogger<CheckinQueueProcessor> logger, ICheckinLists lists)
    {
        _logger = logger;
        _lists = lists;
    }

    public async Task<CheckinResponse> Process(CheckinRequest request)
    {
        var stopwatch = Stopwatch.StartNew();
        var unprocessed = new List<CheckinItem>();
        var results = new List<CheckinResult>();

        var existingFiles = Directory.GetFiles("./data/results").Select(Path.GetFileNameWithoutExtension).ToList();
                    
        foreach (var item in request.Queue)
        {
            using (_logger.BeginScope("Processing check-in item for {date}", item.CheckinFields.Date))
            {
                if (!item.FormResponse.TryGetValue("Feel Well-Rested", out _))
                {
                    _logger.LogInformation("Morning check-in not completed. Skipping...");
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

                var today = item.CheckinFields.Date;

                if (item.GetWeight != null)
                {
                    _logger.LogInformation("Getting weight data..."); // TODO
                }

                if (item.FormResponse.Any(x => _lists.TrackedActivities.Contains(x.Key)))
                {
                    _logger.LogInformation("Getting Strava activity data..."); // TODO
                }

                try
                {
                    var json = item.Serialize().Replace("\\u003C", "<");
                    if (existingFiles.Contains(today))
                    {
                        _logger.LogInformation("Overwriting existing results file");
                    }

                    await File.WriteAllTextAsync($"./data/results/{today}.json", json);
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
            "Processed {processedCount}, skipped {skippedCount} item(s) in {elapsed} ms",
            results.Count,
            unprocessed.Count,
            stopwatch.ElapsedMilliseconds);

        return new CheckinResponse(unprocessed, results);
    }
}

public interface ICheckinQueueProcessor
{
    Task<CheckinResponse> Process(CheckinRequest request);
}
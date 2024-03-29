using System.Text.Json.Serialization;
using Serilog;

namespace CheckinApi.Models;

public class CheckinItem
{
    public CheckinFields CheckinFields { get; }
    public Dictionary<string, string> FormResponse { get; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool GetWeight { get; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? SleepStart { get; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? SleepEnd { get; }

    [JsonConstructor]
    public CheckinItem(
        CheckinFields checkinFields,
        Dictionary<string, string> formResponse,
        bool getWeight,
        long? sleepStart = null,
        long? sleepEnd = null)
    {
        CheckinFields = checkinFields;
        FormResponse = formResponse;
        GetWeight = getWeight;
        SleepStart = sleepStart;
        SleepEnd = sleepEnd;
    }

    public void UpdateTimeInBed()
    {
        if (SleepStart.HasValue && SleepEnd.HasValue)
        {
            var totalTime = TimeSpan.FromSeconds(SleepEnd.Value - SleepStart.Value).ToString(@"h\:mm");
            FormResponse["Total Time in Bed"] = totalTime;
        }
        else
        {
            Log.Logger.Warning("Missing sleep start and/or end time: {sleepStart}, {sleepEnd}", SleepStart, SleepEnd);
        }
    }

    public void UpdateWeightData(List<Weight> weightData)
    {
        var data = weightData.FirstOrDefault(w => w.Date == CheckinFields.Date);
        if (!GetWeight || data == null)
        {
            return;
        }

        FormResponse["BMI"] = data.Bmi.ToString();
        FormResponse["Body fat %"] = data.Fat.ToString();
        FormResponse["Weight (lbs)"] = data.Lbs.ToString();
    }

    public void ProcessActivityData(List<StravaActivity> activityData, List<string> trackedActivities)
    {
        if (activityData.Count == 0)
        {
            return;
        }

        var date = DateTime.Parse(CheckinFields.Date);
        foreach (var activity in trackedActivities)
        {
            var matchingActivities = activityData
                .Where(a => a.Type == activity && a.Date == CheckinFields.Date)
                .ToList();

            if (!matchingActivities.Any())
            {
                continue;
            }

            var activitySums = matchingActivities.Sum(a => a.Distance);
            FormResponse[activity] = activitySums.ToString();
        }
    }

    public string BuildResultsString(List<string> fullChecklist)
    {
        var day = DateTime.Parse(CheckinFields.Date).Day;
        var results = string.Join(",", fullChecklist.Select(x => FormResponse.GetValueOrDefault(x)));

        return $"{day},{results}";
    }
}

using System.Globalization;
using System.Text.Json.Serialization;
using Serilog;

namespace CheckinApi.Models;

public class CheckinItem
{
    public CheckinFields CheckinFields { get; }
    public Dictionary<string, string> FormResponse { get; }
    public string TimeZoneId { get; }

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
        string timeZoneId,
        long? sleepStart = null,
        long? sleepEnd = null)
    {
        CheckinFields = checkinFields;
        FormResponse = formResponse;
        GetWeight = getWeight;
        TimeZoneId = timeZoneId;
        SleepStart = sleepStart;
        SleepEnd = sleepEnd;
    }

    private string? UnixToFormattedTime(long unixTs)
    {
        DateTimeOffset dateTimeOffset = DateTimeOffset.FromUnixTimeSeconds(unixTs);

        try
        {
            // TimeZoneId = e.g., "America/Chicago"
            TimeZoneInfo timeZone = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);
            DateTime targetTime = TimeZoneInfo.ConvertTime(dateTimeOffset.UtcDateTime, TimeZoneInfo.Utc, timeZone);
            string formatted = targetTime.ToString("h:mm:00 tt", CultureInfo.InvariantCulture);
            return formatted;
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "Error converting unix timestamp {unixTs} to formatted time for {timeZoneId} time zone", unixTs, TimeZoneId);
        }

        return null;
    }

    public void UpdateTimeInBed()
    {
        if (SleepStart.HasValue)
        {
            string? formattedTime = UnixToFormattedTime(SleepStart.Value);
            if (formattedTime != null)
            {
                FormResponse["Bedtime"] = formattedTime;
            }
        }

        if (SleepEnd.HasValue)
        {
            string? formattedTime = UnixToFormattedTime(SleepEnd.Value);
            if (formattedTime != null)
            {
                FormResponse["Wake-up time"] = formattedTime;
            }
        }

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

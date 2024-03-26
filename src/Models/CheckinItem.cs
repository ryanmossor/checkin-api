using System.Text.Json.Serialization;

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
        long? sleepStart,
        long? sleepEnd)
    {
        CheckinFields = checkinFields;
        FormResponse = formResponse;
        GetWeight = getWeight;
        SleepStart = sleepStart;
        SleepEnd = sleepEnd;
    }
}

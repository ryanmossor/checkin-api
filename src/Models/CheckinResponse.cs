using System.Text.Json.Serialization;

namespace CheckinApi.Models;

public class CheckinResponse
{
    public List<CheckinItem> Unprocessed { get; set; }
    public List<CheckinResult> Results { get; set; }
    public int ProcessedCount { get; set; }

    [JsonConstructor]
    public CheckinResponse(List<CheckinItem> unprocessed, List<CheckinResult> results)
    {
        Unprocessed = unprocessed;
        Results = results;
        ProcessedCount = results.Count;
    }
}

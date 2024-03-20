using System.Text.Json.Serialization;

namespace CheckinApi.Models;

public class CheckinResponse
{
    public List<CheckinItem> Queue { get; set; }
    public List<CheckinResult> Results { get; set; }
    public int ProcessedCount { get; set; }

    [JsonConstructor]
    public CheckinResponse(List<CheckinItem> queue, List<CheckinResult> results)
    {
        Queue = queue;
        Results = results;
        ProcessedCount = results.Count;
    }
}
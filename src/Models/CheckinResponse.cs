using System.Text.Json.Serialization;

namespace CheckinApi.Models;

public class CheckinResponse
{
    public List<CheckinResult> Results { get; set; }
    public List<CheckinItem> Unprocessed { get; set; }

    [JsonConstructor]
    public CheckinResponse(List<CheckinResult> results, List<CheckinItem>? unprocessed = null)
    {
        Results = results;
        Unprocessed = unprocessed ?? new List<CheckinItem>();
    }
}

using System.Text.Json.Serialization;

namespace CheckinApi.Models;

public class CheckinRequest
{
    public List<CheckinItem> Queue { get; }

    [JsonConstructor]
    public CheckinRequest(List<CheckinItem> queue)
    {
        Queue = queue;
    }
}

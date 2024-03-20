using System.Text.Json.Serialization;

namespace CheckinApi.Models;

public class CheckinRequest
{
    public CheckinItem[] Queue { get; }

    [JsonConstructor]
    public CheckinRequest(CheckinItem[] queue)
    {
        Queue = queue;
    }
}
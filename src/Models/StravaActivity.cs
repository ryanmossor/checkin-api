using System.Text.Json.Serialization;

namespace CheckinApi.Models;

public class StravaActivity
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("distance")]
    public double Distance { get; set; }

    [JsonPropertyName("start_date")]
    public string StartDate { get; set; }

    [JsonPropertyName("start_date_local")]
    public string StartDateLocal { get; set; }

    public string Date { get; set; }

    [JsonConstructor]
    public StravaActivity(string name, string type, double distance, string startDate, string startDateLocal)
    {
        Name = name;
        Type = type;
        Distance = Math.Round(distance / 1609.344, 2); // meters to miles
        Date = startDateLocal.Substring(0, 10); // yyyy-MM-dd
    }
}

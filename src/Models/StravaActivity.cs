using System.Text.Json.Serialization;

namespace CheckinApi.Models.Strava;

public class StravaActivity
{
    [JsonPropertyName("name")]
    public string Name { get; set; }
    
    [JsonPropertyName("type")]
    public string Type { get; set; }
    
    [JsonPropertyName("sport_type")]
    public string SportType { get; set; }
    
    [JsonPropertyName("distance")]
    public double Distance { get; set; }
    
    [JsonPropertyName("average_speed")]
    public double AverageSpeed { get; set; }
    
    [JsonPropertyName("max_speed")]
    public double MaxSpeed { get; set; }
    
    [JsonPropertyName("total_elevation_gain")]
    public double TotalElevationGain { get; set; }
    
    [JsonPropertyName("elapsed_time")]
    public int ElapsedTime { get; set; }
    
    [JsonPropertyName("moving_time")]
    public int MovingTime { get; set; }
    
    [JsonPropertyName("start_date")]
    public string StartDate { get; set; }
    
    [JsonPropertyName("start_date_local")]
    public string StartDateLocal { get; set; }

    [JsonConstructor]
    public StravaActivity(string name, string type, string sportType, double distance, double averageSpeed, double maxSpeed,
        double totalElevationGain, int elapsedTime, int movingTime, string startDate, string startDateLocal)
    {
        Name = name;
        Type = type;
        SportType = sportType;
        Distance = Math.Round(distance / 1609.344, 2); // km to miles
        AverageSpeed = averageSpeed;
        MaxSpeed = maxSpeed;
        TotalElevationGain = totalElevationGain;
        ElapsedTime = elapsedTime;
        MovingTime = movingTime;
        StartDate = startDate;
        StartDateLocal = startDateLocal;
    }
}

using System.Text.Json.Serialization;

namespace CheckinApi.Models;

public class Weight
{
    [JsonPropertyName("bmi")]
    public double Bmi { get; set; }

    [JsonPropertyName("fat")]
    public double Fat { get; set; }

    [JsonPropertyName("weight")]
    public double Lbs { get; set; }

    public Weight(double bmi, double fat, double lbs)
    {
        Bmi = Math.Round(bmi, 1);
        Fat = Math.Round(fat, 1);
        Lbs = Math.Round(lbs * 2.2046, 1); // kg to lbs
    }
}

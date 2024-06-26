using System.Text.Json.Serialization;

namespace CheckinApi.Models;

public class Weight
{
    public string Date { get; set; }
    public double Bmi { get; set; }
    public double Fat { get; set; }
    [JsonPropertyName("weight")]
    public double Lbs { get; set; }

    public Weight(string date, double bmi, double fat, double lbs)
    {
        Date = date;
        Bmi = Math.Round(bmi, 1);
        Fat = Math.Round(fat, 1);
        Lbs = Math.Round(lbs * 2.2046, 1); // kg to lbs
    }
}

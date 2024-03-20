using System.Text.Json.Serialization;

namespace CheckinApi.Models;

public class CheckinFields
{
    public string SpreadsheetName { get; }
    public string Date { get; }
    public string Month { get; }
    public string CellReference { get; }

    [JsonConstructor]
    public CheckinFields(string spreadsheetName, string date, string month, string cellReference)
    {
        SpreadsheetName = spreadsheetName;
        Date = date;
        Month = month;
        CellReference = cellReference;
    }
}
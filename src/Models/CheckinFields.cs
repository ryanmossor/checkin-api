using System.Text.Json.Serialization;

namespace CheckinApi.Models;

public class CheckinFields
{
    public string SpreadsheetId { get; }
    public string Date { get; }
    public string Month { get; }
    public string CellReference { get; }

    [JsonConstructor]
    public CheckinFields(string spreadsheetId, string date, string month, string cellReference)
    {
        SpreadsheetId = spreadsheetId;
        Date = date;
        Month = month;
        CellReference = cellReference;
    }
}

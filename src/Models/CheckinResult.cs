using System.Text.Json.Serialization;

namespace CheckinApi.Models;

public class CheckinResult
{
    public string SpreadsheetId { get; }
    public string Date { get; }
    public string Month { get; }
    public string CellReference { get; }
    public string ResultsString { get; }

    [JsonConstructor]
    public CheckinResult(CheckinFields checkinFields, string resultsString)
    {
        SpreadsheetId = checkinFields.SpreadsheetId;
        Date = checkinFields.Date;
        Month = checkinFields.Month;
        CellReference = checkinFields.CellReference;
        ResultsString = resultsString;
    }
}

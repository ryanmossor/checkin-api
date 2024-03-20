using System.Text.Json.Serialization;

namespace CheckinApi.Models;

public class CheckinResult
{
    public string SpreadsheetName { get; }
    public string Date { get; }
    public string Month { get; }
    public string CellReference { get; }
    public string ResultsString { get; }

    [JsonConstructor]
    public CheckinResult(CheckinFields checkinFields, string resultsString)
    {
        SpreadsheetName = checkinFields.SpreadsheetName;
        Date = checkinFields.Date;
        Month = checkinFields.Month;
        CellReference = checkinFields.CellReference;
        ResultsString = resultsString;
    }
}
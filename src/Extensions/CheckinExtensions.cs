using System.Text;
using CheckinApi.Models;

namespace CheckinApi.Extensions;

public static class CheckinExtensions
{
    public static List<CheckinResult> ConcatenateResults(this List<CheckinResult> checkinResults, string? columnDelimiter = "|")
    {
        var resultsByMonth = checkinResults.GroupBy(r => r.Month).ToList();

        var concatenatedResults = new List<CheckinResult>();
        foreach (var resultGroup in resultsByMonth)
        {
            var dates = resultGroup.Select(r => DateTime.Parse(r.Date)).ToList();
            var startDate = dates.Min();
            var endDate = dates.Max();

            var sb = new StringBuilder();
            for (var date = startDate; date <= endDate; date = date.AddDays(1))
            {
                if (sb.Length > 0)
                {
                    sb.Append(columnDelimiter);
                }

                sb.Append(date.Day);

                var matchingResult = resultGroup.FirstOrDefault(r => r.Date == date.ToString("yyyy-MM-dd"));
                if (matchingResult != null)
                {
                    sb.Append($",{matchingResult.ResultsString}");
                }
            }

            var firstRes = resultGroup.First();
            var result = new CheckinResult(
                new CheckinFields(firstRes.SpreadsheetName, firstRes.Date, firstRes.Month, firstRes.CellReference),
                resultsString: sb.ToString());

            concatenatedResults.Add(result);
        }

        return concatenatedResults;
    }
}

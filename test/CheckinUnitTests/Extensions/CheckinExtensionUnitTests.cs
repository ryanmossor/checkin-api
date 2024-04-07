using CheckinApi.Extensions;
using CheckinApi.Models;

namespace CheckinUnitTests.Extensions;

public class CheckinExtensionUnitTests
{
    [Fact]
    public void should_not_concat_single_result()
    {
        // arrange
        var checkinResults = new List<CheckinResult>()
        {
            new CheckinResult(new CheckinFields("sheet", "2024-03-28", "Mar", "AD1"), "28,1,,2,,3"),
        };

        // act
        var result = checkinResults.ConcatenateResults();

        // assert
        Assert.NotNull(result);
        Assert.Single(result);

        Assert.Equivalent(
            new { result[0].SpreadsheetName, result[0].Month, result[0].Date, result[0].CellReference },
            new { checkinResults[0].SpreadsheetName, checkinResults[0].Month, checkinResults[0].Date, checkinResults[0].CellReference });
        Assert.Equal("28,1,,2,,3", result[0].ResultsString);
    }

    [Fact]
    public void should_concat_checkin_results_with_default_delimiter()
    {
        // arrange
        var checkinResults = new List<CheckinResult>()
        {
            new CheckinResult(new CheckinFields("sheet", "2024-03-27", "Mar", "AC1"), "27,1,2,3,4,5"),
            new CheckinResult(new CheckinFields("sheet", "2024-03-28", "Mar", "AD1"), "28,,1,2,3,4"),
        };

        // act
        var result = checkinResults.ConcatenateResults();

        // assert
        Assert.NotNull(result);
        Assert.Single(result);

        Assert.Equivalent(
            new { result[0].SpreadsheetName, result[0].Month, result[0].Date, result[0].CellReference },
            new { checkinResults[0].SpreadsheetName, checkinResults[0].Month, checkinResults[0].Date, checkinResults[0].CellReference });
        Assert.Equal("27,1,2,3,4,5|28,,1,2,3,4", result[0].ResultsString);
    }

    [Fact]
    public void should_concat_checkin_results_with_custom_delimiter()
    {
        // arrange
        var checkinResults = new List<CheckinResult>()
        {
            new CheckinResult(new CheckinFields("sheet", "2024-03-27", "Mar", "AC1"), "27,1,2,3,4,5"),
            new CheckinResult(new CheckinFields("sheet", "2024-03-28", "Mar", "AD1"), "28,,1,2,3,4"),
        };

        // act
        var result = checkinResults.ConcatenateResults(columnDelimiter: "=:=");

        // assert
        Assert.NotNull(result);
        Assert.Single(result);

        Assert.Equivalent(
            new { result[0].SpreadsheetName, result[0].Month, result[0].Date, result[0].CellReference },
            new { checkinResults[0].SpreadsheetName, checkinResults[0].Month, checkinResults[0].Date, checkinResults[0].CellReference });
        Assert.Equal("27,1,2,3,4,5=:=28,,1,2,3,4", result[0].ResultsString);
    }

    [Fact]
    public void should_include_day_but_no_results_string_for_skipped_days()
    {
        // arrange
        var checkinResults = new List<CheckinResult>()
        {
            new CheckinResult(new CheckinFields("sheet", "2024-03-24", "Mar", "Z1"), "24,1,2,3,4,5"),
            new CheckinResult(new CheckinFields("sheet", "2024-03-28", "Mar", "AD1"), "28,,1,2,3,4"),
        };

        // act
        var result = checkinResults.ConcatenateResults();

        // assert
        Assert.NotNull(result);
        Assert.Single(result);

        Assert.Equivalent(
            new { result[0].SpreadsheetName, result[0].Month, result[0].Date, result[0].CellReference },
            new { checkinResults[0].SpreadsheetName, checkinResults[0].Month, checkinResults[0].Date, checkinResults[0].CellReference });
        Assert.Equal("24,1,2,3,4,5|25|26|27|28,,1,2,3,4", result[0].ResultsString);
    }

    [Fact]
    public void should_return_separate_concat_results_by_month()
    {
        // arrange
        var checkinResults = new List<CheckinResult>()
        {
            new CheckinResult(new CheckinFields("sheet", "2024-03-27", "Mar", "AC1"), "27,1,2,3,4,5"),
            new CheckinResult(new CheckinFields("sheet", "2024-03-28", "Mar", "AD1"), "28,,1,2,3,4"),
            new CheckinResult(new CheckinFields("sheet", "2024-04-12", "Apr", "N1"), "12,,,1,2,3"),
        };

        // act
        var result = checkinResults.ConcatenateResults();

        // assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);

        Assert.Equivalent(
            new { result[0].SpreadsheetName, result[0].Month, result[0].Date, result[0].CellReference },
            new { checkinResults[0].SpreadsheetName, checkinResults[0].Month, checkinResults[0].Date, checkinResults[0].CellReference });
        Assert.Equal("27,1,2,3,4,5|28,,1,2,3,4", result[0].ResultsString);

        Assert.Equivalent(
            new { result[1].SpreadsheetName, result[1].Month, result[1].Date, result[1].CellReference },
            new { checkinResults[2].SpreadsheetName, checkinResults[2].Month, checkinResults[2].Date, checkinResults[2].CellReference });
        Assert.Equal("12,,,1,2,3", result[1].ResultsString);
    }
}

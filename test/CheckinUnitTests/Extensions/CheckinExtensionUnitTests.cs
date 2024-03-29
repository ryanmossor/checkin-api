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
            new CheckinResult(new CheckinFields("sheet", "2024-03-28", "Mar", "AD1"), "1,,2,,3"),
        };

        // act
        var result = checkinResults.ConcatenateResults();

        // assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);

        result[0].SpreadsheetName.ShouldBe(checkinResults[0].SpreadsheetName);
        result[0].Date.ShouldBe(checkinResults[0].Date);
        result[0].Month.ShouldBe(checkinResults[0].Month);
        result[0].CellReference.ShouldBe(checkinResults[0].CellReference);
        result[0].ResultsString.ShouldBe("28,1,,2,,3");
    }

    [Fact]
    public void should_concat_checkin_results_with_default_delimiter()
    {
        // arrange
        var checkinResults = new List<CheckinResult>()
        {
            new CheckinResult(new CheckinFields("sheet", "2024-03-27", "Mar", "AC1"), "1,2,3,4,5"),
            new CheckinResult(new CheckinFields("sheet", "2024-03-28", "Mar", "AD1"), ",1,2,3,4"),
        };

        // act
        var result = checkinResults.ConcatenateResults();

        // assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        result[0].ResultsString.ShouldBe("27,1,2,3,4,5|28,,1,2,3,4");
    }

    [Fact]
    public void should_concat_checkin_results_with_custom_delimiter()
    {
        // arrange
        var checkinResults = new List<CheckinResult>()
        {
            new CheckinResult(new CheckinFields("sheet", "2024-03-27", "Mar", "AC1"), "1,2,3,4,5"),
            new CheckinResult(new CheckinFields("sheet", "2024-03-28", "Mar", "AD1"), ",1,2,3,4"),
        };

        // act
        var result = checkinResults.ConcatenateResults(columnDelimiter: "=:=");

        // assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        result[0].ResultsString.ShouldBe("27,1,2,3,4,5=:=28,,1,2,3,4");
    }

    [Fact]
    public void should_include_day_but_no_results_string_for_skipped_days()
    {
        // arrange
        var checkinResults = new List<CheckinResult>()
        {
            new CheckinResult(new CheckinFields("sheet", "2024-03-24", "Mar", "Z1"), "1,2,3,4,5"),
            new CheckinResult(new CheckinFields("sheet", "2024-03-28", "Mar", "AD1"), ",1,2,3,4"),
        };

        // act
        var result = checkinResults.ConcatenateResults();

        // assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        result[0].ResultsString.ShouldBe("24,1,2,3,4,5|25|26|27|28,,1,2,3,4");
    }

    [Fact]
    public void should_return_separate_concat_results_by_month()
    {
        // arrange
        var checkinResults = new List<CheckinResult>()
        {
            new CheckinResult(new CheckinFields("sheet", "2024-03-27", "Mar", "AC1"), "1,2,3,4,5"),
            new CheckinResult(new CheckinFields("sheet", "2024-03-28", "Mar", "AD1"), ",1,2,3,4"),
            new CheckinResult(new CheckinFields("sheet", "2024-04-12", "Apr", "N1"), ",,1,2,3"),
        };

        // act
        var result = checkinResults.ConcatenateResults();

        // assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(2);

        result[0].SpreadsheetName.ShouldBe(checkinResults[0].SpreadsheetName);
        result[0].Date.ShouldBe(checkinResults[0].Date);
        result[0].Month.ShouldBe(checkinResults[0].Month);
        result[0].CellReference.ShouldBe(checkinResults[0].CellReference);
        result[0].ResultsString.ShouldBe("27,1,2,3,4,5|28,,1,2,3,4");

        result[1].SpreadsheetName.ShouldBe(checkinResults[2].SpreadsheetName);
        result[1].Date.ShouldBe(checkinResults[2].Date);
        result[1].Month.ShouldBe(checkinResults[2].Month);
        result[1].CellReference.ShouldBe(checkinResults[2].CellReference);
        result[1].ResultsString.ShouldBe("12,,,1,2,3");
    }
}

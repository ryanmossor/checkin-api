using CheckinApi.Interfaces;
using CheckinApi.Models;
using CheckinApi.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace CheckinUnitTests.Services;

public partial class CheckinQueueProcessorUnitTests
{
    public CheckinQueueProcessor Setup(List<CheckinItem>? mockCheckinItems = null)
    {
        var repository = Substitute.For<ICheckinRepository>();

        repository.GetCheckinItemsAsync(Arg.Any<List<string>>())
            .Returns(mockCheckinItems ?? new List<CheckinItem>());

        var savedResultDates = new List<string?>
            { "2024-03-28", "2024-03-30", "2024-04-04", "2024-04-05", "2024-04-06", "2024-04-07" };
        repository.GetAllCheckinDates()
            .Returns(savedResultDates);

        var checklists = new CheckinLists(
            fullChecklist: new List<string> { "Habit 1", "Habit 2", "Habit 3", "Habit 4", "Habit 5" },
            trackedActivities: new List<string> { "Hike", "Kayaking", "Ride", "Run" });
        repository.GetCheckinListsAsync()
            .Returns(checklists);

        var processor = new CheckinQueueProcessor(
            Substitute.For<IActivityService>(),
            Substitute.For<IHealthTrackingService>(),
            Substitute.For<ILogger<CheckinQueueProcessor>>(),
            repository);

        return processor;
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("2024-03-31")]
    [InlineData("2024-03-31,2024-04-01")]
    public async Task should_return_no_results_when_no_saved_data_for_provided_dates(string dates)
    {
        // arrange
        var processor = Setup();

        // act
        var result = await processor.ProcessSavedResultsAsync(dates, concatResults: false, delimiter: null);

        // assert
        Assert.NotNull(result);
        Assert.Empty(result.Results);
    }

    [Theory]
    [InlineData("2024-04-06,2024-04-07")]
    [InlineData("invalid,2024-04-06,2024-04-07,more invalid")]
    public async Task should_return_results_for_dates_with_saved_data(string dates)
    {
        // arrange
        var mockCheckinItems = new List<CheckinItem>()
        {
            new CheckinItem(
                checkinFields: new CheckinFields("sheet", "2024-04-06", "Apr", "H1"),
                formResponse: new Dictionary<string, string>() { ["Habit 1"] = "1", ["Habit 2"] = "1", ["Habit 5"] = "1" },
                getWeight: false),
            new CheckinItem(
                checkinFields: new CheckinFields("sheet", "2024-04-07", "Apr", "I1"),
                formResponse: new Dictionary<string, string>() { ["Habit 3"] = "1", ["Habit 5"] = "1" },
                getWeight: false),
        };
        var processor = Setup(mockCheckinItems);

        // act
        var result = await processor.ProcessSavedResultsAsync(dates, concatResults: false, delimiter: null);

        // assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Results.Count);

        Assert.Equivalent(
            new { result.Results[0].SpreadsheetId, result.Results[0].Month, result.Results[0].Date, result.Results[0].CellReference },
            mockCheckinItems[0].CheckinFields);
        Assert.Equal("6,1,1,,,1", result.Results[0].ResultsString);

        Assert.Equivalent(
            new { result.Results[1].SpreadsheetId, result.Results[1].Month, result.Results[1].Date, result.Results[1].CellReference },
            mockCheckinItems[1].CheckinFields);
        Assert.Equal("7,,,1,,1", result.Results[1].ResultsString);
    }

    [Fact]
    public async Task should_return_concatenated_results_from_saved_items()
    {
        // arrange
        var mockCheckinItems = new List<CheckinItem>()
        {
            new CheckinItem(
                checkinFields: new CheckinFields("sheet", "2024-03-28", "Mar", "AD1"),
                formResponse: new Dictionary<string, string>() { ["Habit 2"] = "1", ["Habit 4"] = "1", ["Habit 5"] = "1" },
                getWeight: false),
            new CheckinItem(
                checkinFields: new CheckinFields("sheet", "2024-03-30", "Mar", "AF1"),
                formResponse: new Dictionary<string, string>() { ["Habit 4"] = "1", ["Habit 5"] = "1" },
                getWeight: false),
            new CheckinItem(
                checkinFields: new CheckinFields("sheet", "2024-04-04", "Apr", "F1"),
                formResponse: new Dictionary<string, string>() { ["Habit 1"] = "1", ["Habit 2"] = "1", ["Habit 5"] = "1" },
                getWeight: false),
            new CheckinItem(
                checkinFields: new CheckinFields("sheet", "2024-04-07", "Apr", "I1"),
                formResponse: new Dictionary<string, string>() { ["Habit 3"] = "1", ["Habit 5"] = "1" },
                getWeight: false),
        };
        var processor = Setup(mockCheckinItems);

        // act
        var result = await processor.ProcessSavedResultsAsync(
            dates: "2024-03-28,2024-03-30,2024-04-04,2024-04-07",
            concatResults: true,
            delimiter: null);

        // assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Results.Count);

        Assert.Equivalent(
            new { result.Results[0].SpreadsheetId, result.Results[0].Month, result.Results[0].Date, result.Results[0].CellReference },
            mockCheckinItems[0].CheckinFields);
        Assert.Equal("28,,1,,1,1|29|30,,,,1,1", result.Results[0].ResultsString);

        Assert.Equivalent(
            new { result.Results[1].SpreadsheetId, result.Results[1].Month, result.Results[1].Date, result.Results[1].CellReference },
            mockCheckinItems[2].CheckinFields);
        Assert.Equal("4,1,1,,,1|5|6|7,,,1,,1", result.Results[1].ResultsString);
    }
}

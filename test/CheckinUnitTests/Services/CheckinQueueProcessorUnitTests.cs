using CheckinApi.Interfaces;
using CheckinApi.Models;
using CheckinApi.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace CheckinUnitTests.Services;

public partial class CheckinQueueProcessorUnitTests
{
    private CheckinQueueProcessor SetupProcessor(List<Weight>? mockWeightData = null, List<StravaActivity>? mockActivityData = null)
    {
        var healthService = Substitute.For<IHealthTrackingService>();
        healthService.GetWeightDataAsync(Arg.Any<List<CheckinItem>>())
            .Returns(mockWeightData ?? new List<Weight>());

        var activityService = Substitute.For<IActivityService>();
        activityService.GetActivityDataAsync(Arg.Any<List<CheckinItem>>())
            .Returns(mockActivityData ?? new List<StravaActivity>());

        var checklists = new CheckinLists(
            fullChecklist: new List<string>
            {
                "Journal", "Read", "Hike", "Run", "Bedtime", "Wake-up time", "Feel Well-Rested", "BMI", "Body fat %", "Weight (lbs)"
            },
            trackedActivities: new List<string> { "Hike", "Kayaking", "Ride", "Run" });

        var repository = Substitute.For<ICheckinRepository>();
        repository.GetCheckinListsAsync()
            .Returns(checklists);

        var processor = new CheckinQueueProcessor(
            activityService,
            healthService,
            Substitute.For<ILogger<CheckinQueueProcessor>>(),
            repository);

        return processor;
    }

    [Fact]
    public async Task should_process_single_item_queue_no_weight_or_activities()
    {
        // arrange
        var item1 = new CheckinItem(
            checkinFields: new CheckinFields("sheet", "2024-03-31", "Mar", "AG1"),
            formResponse: new Dictionary<string, string>()
            {
                ["Journal"] = "1",
                ["Bedtime"] = "9:14:00 PM",
                ["Wake-up time"] = "4:57:00 PM",
                ["Feel Well-Rested"] = "4"
            },
            getWeight: false,
            sleepStart: 1709954057,
            sleepEnd: 1709981828);
        var queue = new List<CheckinItem> { item1 };
        var processor = SetupProcessor();

        // act
        var result = await processor.ProcessQueueAsync(queue, concatResults: false, forceProcessing: false, delimiter: null);

        // assert
        Assert.NotNull(result);
        Assert.Empty(result.Unprocessed);
        Assert.Single(result.Results);

        Assert.Equivalent(
            new { result.Results[0].SpreadsheetId, result.Results[0].Month, result.Results[0].Date, result.Results[0].CellReference },
            item1.CheckinFields);
        Assert.Equal("31,1,,,,9:14:00 PM,4:57:00 PM,4,,,", result.Results[0].ResultsString);
    }

    [Fact]
    public async Task should_not_process_if_morning_checkin_not_completed()
    {
        // arrange
        var item1 = new CheckinItem(
            checkinFields: new CheckinFields("sheet", "2024-03-31", "Mar", "AG1"),
            // missing "Feel Well-Rested" -> morning check-in not completed
            formResponse: new Dictionary<string, string>() { ["Journal"] = "1" },
            getWeight: false);
        var queue = new List<CheckinItem> { item1 };
        var processor = SetupProcessor();

        // act
        var result = await processor.ProcessQueueAsync(queue, concatResults: false, forceProcessing: false, delimiter: null);

        // assert
        Assert.NotNull(result);
        Assert.Empty(result.Results);
        Assert.Single(result.Unprocessed);
    }

    [Fact]
    public async Task should_not_process_if_getWeight_true_but_cannot_retrieve_weight_data()
    {
        // arrange
        var item1 = new CheckinItem(
            checkinFields: new CheckinFields("sheet", "2024-03-31", "Mar", "AG1"),
            formResponse: new Dictionary<string, string>() { ["Feel Well-Rested"] = "4" },
            getWeight: true);
        var queue = new List<CheckinItem> { item1 };
        var processor = SetupProcessor();

        // act
        var result = await processor.ProcessQueueAsync(queue, concatResults: false, forceProcessing: false, delimiter: null);

        // assert
        Assert.NotNull(result);
        Assert.Empty(result.Results);
        Assert.Single(result.Unprocessed);
    }

    [Fact]
    public async Task should_not_process_if_tracked_activities_in_queue_and_cannot_retrieve_activity_data()
    {
        // arrange
        var item1 = new CheckinItem(
            checkinFields: new CheckinFields("sheet", "2024-03-31", "Mar", "AG1"),
            formResponse: new Dictionary<string, string>() { ["Hike"] = "1", ["Feel Well-Rested"] = "4" },
            getWeight: false);
        var queue = new List<CheckinItem> { item1 };
        var processor = SetupProcessor();

        // act
        var result = await processor.ProcessQueueAsync(queue, concatResults: false, forceProcessing: false, delimiter: null);

        // assert
        Assert.NotNull(result);
        Assert.Empty(result.Results);
        Assert.Single(result.Unprocessed);
    }

    [Fact]
    public async Task should_process_and_include_weight_data_for_single_queue_item()
    {
        // arrange
        var item1 = new CheckinItem(
            checkinFields: new CheckinFields("sheet", "2024-03-31", "Mar", "AG1"),
            formResponse: new Dictionary<string, string>() { ["Feel Well-Rested"] = "4" },
            getWeight: true);
        var queue = new List<CheckinItem> { item1 };
        var weightData = new List<Weight> { new Weight("2024-03-31", 24.5d, 17.2d, 85.6d) };
        var processor = SetupProcessor(mockWeightData: weightData);

        // act
        var result = await processor.ProcessQueueAsync(queue, concatResults: false, forceProcessing: false, delimiter: null);

        // assert
        Assert.NotNull(result);
        Assert.Empty(result.Unprocessed);
        Assert.Single(result.Results);

        Assert.Equivalent(
            new { result.Results[0].SpreadsheetId, result.Results[0].Month, result.Results[0].Date, result.Results[0].CellReference },
            item1.CheckinFields);
        Assert.Equal("31,,,,,,,4,24.5,17.2,188.7", result.Results[0].ResultsString);
    }

    [Fact]
    public async Task should_process_and_include_weight_data_for_multiple_queue_items()
    {
        // arrange
        var item1 = new CheckinItem(
            checkinFields: new CheckinFields("sheet", "2024-03-25", "Mar", "AA1"),
            formResponse: new Dictionary<string, string>() { ["Feel Well-Rested"] = "3" },
            getWeight: true);
        var item2 = new CheckinItem(
            checkinFields: new CheckinFields("sheet", "2024-03-31", "Mar", "AG1"),
            formResponse: new Dictionary<string, string>() { ["Feel Well-Rested"] = "4" },
            getWeight: true);
        var queue = new List<CheckinItem> { item1, item2 };
        var weightData = new List<Weight>
        {
            new Weight("2024-03-25", 24.2d, 16.8d, 84.7d),
            new Weight("2024-03-31", 24.5d, 17.2d, 85.6d),
        };
        var processor = SetupProcessor(mockWeightData: weightData);

        // act
        var result = await processor.ProcessQueueAsync(queue, concatResults: false, forceProcessing: false, delimiter: null);

        // assert
        Assert.NotNull(result);
        Assert.Empty(result.Unprocessed);
        Assert.Equal(2, result.Results.Count);

        Assert.Equivalent(
            new { result.Results[0].SpreadsheetId, result.Results[0].Month, result.Results[0].Date, result.Results[0].CellReference },
            item1.CheckinFields);
        Assert.Equal("25,,,,,,,3,24.2,16.8,186.7", result.Results[0].ResultsString);

        Assert.Equivalent(
            new { result.Results[1].SpreadsheetId, result.Results[1].Month, result.Results[1].Date, result.Results[1].CellReference },
            item2.CheckinFields);
        Assert.Equal("31,,,,,,,4,24.5,17.2,188.7", result.Results[1].ResultsString);
    }

    [Fact]
    public async Task should_process_and_include_activity_data_for_single_queue_item()
    {
        // arrange
        var item1 = new CheckinItem(
            checkinFields: new CheckinFields("sheet", "2024-03-31", "Mar", "AG1"),
            formResponse: new Dictionary<string, string>() { ["Hike"] = "1", ["Feel Well-Rested"] = "3" },
            getWeight: false);
        var queue = new List<CheckinItem> { item1 };
        var activityData = new List<StravaActivity>
        {
            new StravaActivity("3/31 Afternoon Hike", "Hike", 13840.4d, "2024-03-31T17:11:48Z", "2024-03-31T12:11:48Z")
        };
        var processor = SetupProcessor(mockActivityData: activityData);

        // act
        var result = await processor.ProcessQueueAsync(queue, concatResults: false, forceProcessing: false, delimiter: null);

        // assert
        Assert.NotNull(result);
        Assert.Empty(result.Unprocessed);
        Assert.Single(result.Results);

        Assert.Equivalent(
            new { result.Results[0].SpreadsheetId, result.Results[0].Month, result.Results[0].Date, result.Results[0].CellReference },
            item1.CheckinFields);
        Assert.Equal("31,,,8.6,,,,3,,,", result.Results[0].ResultsString);
    }

    [Fact]
    public async Task should_process_and_include_activity_data_for_multiple_queue_items()
    {
        // arrange
        var item1 = new CheckinItem(
            checkinFields: new CheckinFields("sheet", "2024-03-30", "Mar", "AF1"),
            formResponse: new Dictionary<string, string>() { ["Hike"] = "1", ["Feel Well-Rested"] = "3" },
            getWeight: false);
        var item2 = new CheckinItem(
            checkinFields: new CheckinFields("sheet", "2024-03-31", "Mar", "AG1"),
            formResponse: new Dictionary<string, string>() { ["Hike"] = "1", ["Feel Well-Rested"] = "3" },
            getWeight: false);
        var queue = new List<CheckinItem> { item1, item2 };
        var activityData = new List<StravaActivity>
        {
            new StravaActivity("3/30 Afternoon Hike", "Hike", 7563.9d, "2024-03-30T17:11:48Z", "2024-03-30T12:11:48Z"),
            new StravaActivity("3/31 Afternoon Hike", "Hike", 13840.4d, "2024-03-31T17:11:48Z", "2024-03-31T12:11:48Z")
        };
        var processor = SetupProcessor(mockActivityData: activityData);

        // act
        var result = await processor.ProcessQueueAsync(queue, concatResults: false, forceProcessing: false, delimiter: null);

        // assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Results.Count);
        Assert.Empty(result.Unprocessed);

        Assert.Equivalent(
            new { result.Results[0].SpreadsheetId, result.Results[0].Month, result.Results[0].Date, result.Results[0].CellReference },
            item1.CheckinFields);
        Assert.Equal("30,,,4.7,,,,3,,,", result.Results[0].ResultsString);

        Assert.Equivalent(
            new { result.Results[1].SpreadsheetId, result.Results[1].Month, result.Results[1].Date, result.Results[1].CellReference },
            item2.CheckinFields);
        Assert.Equal("31,,,8.6,,,,3,,,", result.Results[1].ResultsString);
    }

    [Fact]
    public async Task should_process_and_sum_multiple_activities_of_same_type_for_single_queue_item()
    {
        // arrange
        var item1 = new CheckinItem(
            checkinFields: new CheckinFields("sheet", "2024-03-30", "Mar", "AF1"),
            formResponse: new Dictionary<string, string>() { ["Hike"] = "1", ["Feel Well-Rested"] = "3" },
            getWeight: false);
        var queue = new List<CheckinItem> { item1 };
        var activityData = new List<StravaActivity>
        {
            new StravaActivity("3/30 Afternoon Hike", "Hike", 7563.9d, "2024-03-30T12:11:48Z", "2024-03-30T07:11:48Z"),
            new StravaActivity("3/30 Afternoon Hike", "Hike", 13840.4d, "2024-03-30T17:11:48Z", "2024-03-30T12:11:48Z")
        };
        var processor = SetupProcessor(mockActivityData: activityData);

        // act
        var result = await processor.ProcessQueueAsync(queue, concatResults: false, forceProcessing: false, delimiter: null);

        // assert
        Assert.NotNull(result);
        Assert.Empty(result.Unprocessed);
        Assert.Single(result.Results);

        Assert.Equivalent(
            new { result.Results[0].SpreadsheetId, result.Results[0].Month, result.Results[0].Date, result.Results[0].CellReference },
            item1.CheckinFields);
        Assert.Equal("30,,,13.3,,,,3,,,", result.Results[0].ResultsString);
    }

    [Fact]
    public async Task should_process_and_include_activities_of_different_types()
    {
        // arrange
        var item1 = new CheckinItem(
            checkinFields: new CheckinFields("sheet", "2024-03-30", "Mar", "AF1"),
            formResponse: new Dictionary<string, string>() { ["Hike"] = "1", ["Run"] = "1", ["Feel Well-Rested"] = "3" },
            getWeight: false);
        var queue = new List<CheckinItem> { item1 };
        var activityData = new List<StravaActivity>
        {
            new StravaActivity("3/30 Afternoon Run", "Run", 7563.9d, "2024-03-30T12:11:48Z", "2024-03-30T07:11:48Z"),
            new StravaActivity("3/30 Afternoon Hike", "Hike", 13840.4d, "2024-03-30T17:11:48Z", "2024-03-30T12:11:48Z")
        };
        var processor = SetupProcessor(mockActivityData: activityData);

        // act
        var result = await processor.ProcessQueueAsync(queue, concatResults: false, forceProcessing: false, delimiter: null);

        // assert
        Assert.NotNull(result);
        Assert.Empty(result.Unprocessed);
        Assert.Single(result.Results);

        Assert.Equivalent(
            new { result.Results[0].SpreadsheetId, result.Results[0].Month, result.Results[0].Date, result.Results[0].CellReference },
            item1.CheckinFields);
        Assert.Equal("30,,,8.6,4.7,,,3,,,", result.Results[0].ResultsString);
    }

    [Fact]
    public async Task should_concatenate_multiple_results_strings_from_same_month()
    {
        // arrange
        var item1 = new CheckinItem(
            checkinFields: new CheckinFields("sheet", "2024-03-30", "Mar", "AF1"),
            formResponse: new Dictionary<string, string>() { ["Feel Well-Rested"] = "3" },
            getWeight: false);
        var item2 = new CheckinItem(
            checkinFields: new CheckinFields("sheet", "2024-03-31", "Mar", "AG1"),
            formResponse: new Dictionary<string, string>() { ["Feel Well-Rested"] = "4" },
            getWeight: false);
        var queue = new List<CheckinItem> { item1, item2 };
        var processor = SetupProcessor();

        // act
        var result = await processor.ProcessQueueAsync(queue, concatResults: true, forceProcessing: false, delimiter: null);

        // assert
        Assert.NotNull(result);
        Assert.Single(result.Results);
        Assert.Empty(result.Unprocessed);

        Assert.Equivalent(
            new { result.Results[0].SpreadsheetId, result.Results[0].Month, result.Results[0].Date, result.Results[0].CellReference },
            item1.CheckinFields);
        Assert.Equal("30,,,,,,,3,,,|31,,,,,,,4,,,", result.Results[0].ResultsString);
    }

    [Fact]
    public async Task should_return_multiple_results_and_concatenate_for_items_from_different_months()
    {
        // arrange
        var item1 = new CheckinItem(
            checkinFields: new CheckinFields("sheet", "2024-03-30", "Mar", "AF1"),
            formResponse: new Dictionary<string, string>() { ["Feel Well-Rested"] = "3" },
            getWeight: false);
        var item2 = new CheckinItem(
            checkinFields: new CheckinFields("sheet", "2024-03-31", "Mar", "AG1"),
            formResponse: new Dictionary<string, string>() { ["Feel Well-Rested"] = "4" },
            getWeight: false);
        var item3 = new CheckinItem(
            checkinFields: new CheckinFields("sheet", "2024-04-01", "Apr", "C1"),
            formResponse: new Dictionary<string, string>() { ["Feel Well-Rested"] = "2" },
            getWeight: false);
        var item4 = new CheckinItem(
            checkinFields: new CheckinFields("sheet", "2024-04-02", "Apr", "D1"),
            formResponse: new Dictionary<string, string>() { ["Feel Well-Rested"] = "3" },
            getWeight: false);
        var queue = new List<CheckinItem> { item1, item2, item3, item4 };
        var processor = SetupProcessor();

        // act
        var result = await processor.ProcessQueueAsync(queue, concatResults: true, forceProcessing: false, delimiter: null);

        // assert
        Assert.NotNull(result);
        Assert.Empty(result.Unprocessed);
        Assert.Equal(2, result.Results.Count);

        Assert.Equivalent(
            new { result.Results[0].SpreadsheetId, result.Results[0].Month, result.Results[0].Date, result.Results[0].CellReference },
            item1.CheckinFields);
        Assert.Equal("30,,,,,,,3,,,|31,,,,,,,4,,,", result.Results[0].ResultsString);

        Assert.Equivalent(
            new { result.Results[1].SpreadsheetId, result.Results[1].Month, result.Results[1].Date, result.Results[1].CellReference },
            item3.CheckinFields);
        Assert.Equal("1,,,,,,,2,,,|2,,,,,,,3,,,", result.Results[1].ResultsString);
    }

    [Fact]
    public async Task should_concatenate_results_with_missing_days()
    {
        // arrange
        var item1 = new CheckinItem(
            checkinFields: new CheckinFields("sheet", "2024-03-28", "Mar", "AD1"),
            formResponse: new Dictionary<string, string>() { ["Feel Well-Rested"] = "3" },
            getWeight: false);
        var item2 = new CheckinItem(
            checkinFields: new CheckinFields("sheet", "2024-03-31", "Mar", "AG1"),
            formResponse: new Dictionary<string, string>() { ["Feel Well-Rested"] = "4" },
            getWeight: false);
        var queue = new List<CheckinItem> { item1, item2 };
        var processor = SetupProcessor();

        // act
        var result = await processor.ProcessQueueAsync(queue, concatResults: true, forceProcessing: false, delimiter: null);

        // assert
        Assert.NotNull(result);
        Assert.Empty(result.Unprocessed);
        Assert.Single(result.Results);

        Assert.Equivalent(
            new { result.Results[0].SpreadsheetId, result.Results[0].Month, result.Results[0].Date, result.Results[0].CellReference },
            item1.CheckinFields);
        Assert.Equal("28,,,,,,,3,,,|29|30|31,,,,,,,4,,,", result.Results[0].ResultsString);
    }

    [Fact]
    public async Task should_process_items_that_would_normally_be_skipped_if_forceProcessing_true()
    {
        // arrange
        var item1 = new CheckinItem(
            checkinFields: new CheckinFields("sheet", "2024-03-30", "Mar", "AF1"),
            // tracked activity but no results from API
            formResponse: new Dictionary<string, string>() { ["Hike"] = "1", ["Feel Well-Rested"] = "3" },
            getWeight: false);
        var item2 = new CheckinItem(
            checkinFields: new CheckinFields("sheet", "2024-03-31", "Mar", "AG1"),
            formResponse: new Dictionary<string, string>() { ["Read"] = "1", ["Feel Well-Rested"] = "3" },
            getWeight: true); // getWeight true but weight no results from API
        var item3 = new CheckinItem(
            checkinFields: new CheckinFields("sheet", "2024-04-01", "Apr", "C1"),
            formResponse: new Dictionary<string, string>() { ["Journal"] = "1" }, // missing "Feel Well-Rested"
            getWeight: false);
        var queue = new List<CheckinItem> { item1, item2, item3 };
        var processor = SetupProcessor();

        // act
        var result = await processor.ProcessQueueAsync(queue, concatResults: false, forceProcessing: true, delimiter: null);

        // assert
        Assert.NotNull(result);
        Assert.Empty(result.Unprocessed);
        Assert.Equal(3, result.Results.Count);

        Assert.Equivalent(
            new { result.Results[0].SpreadsheetId, result.Results[0].Month, result.Results[0].Date, result.Results[0].CellReference },
            item1.CheckinFields);
        Assert.Equal("30,,,1,,,,3,,,", result.Results[0].ResultsString);

        Assert.Equivalent(
            new { result.Results[1].SpreadsheetId, result.Results[1].Month, result.Results[1].Date, result.Results[1].CellReference },
            item2.CheckinFields);
        Assert.Equal("31,,1,,,,,3,,,", result.Results[1].ResultsString);

        Assert.Equivalent(
            new { result.Results[2].SpreadsheetId, result.Results[2].Month, result.Results[2].Date, result.Results[2].CellReference },
            item3.CheckinFields);
        Assert.Equal("1,1,,,,,,,,,", result.Results[2].ResultsString);
    }
}

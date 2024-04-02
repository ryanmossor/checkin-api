using CheckinApi.Interfaces;
using CheckinApi.Models;
using CheckinApi.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace CheckinUnitTests.Services;

public class CheckinQueueProcessorUnitTests
{
    public CheckinQueueProcessor SetupProcessor(bool returnWeightData = false, bool returnActivityData = false)
    {
        var healthService = Substitute.For<IHealthTrackingService>();
        var weightData = new List<Weight>();
        if (returnWeightData)
        {
            weightData.Add(new Weight("2024-03-31", 24.5d, 17.2d, 85.6d));
        }

        healthService.GetWeightDataAsync(Arg.Any<List<CheckinItem>>())
            .Returns(weightData);

        var activityData = new List<StravaActivity>();
        var activityService = Substitute.For<IActivityService>();
        if (returnActivityData)
        {
            activityData.Add(
                new StravaActivity("3/31 Afternoon Hike", "Hike", 13840.4d, "2024-03-31T17:11:;8Z", "2024-03-31T12:11:48Z"));
        }

        activityService.GetActivityDataAsync(Arg.Any<List<CheckinItem>>())
            .Returns(activityData);

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
        result.ShouldNotBeNull();
        result.Results.Count.ShouldBe(1);
        result.Results[0].ResultsString.ShouldBe("31,1,,,,9:14:00 PM,4:57:00 PM,4,,,");
        result.Unprocessed.Count.ShouldBe(0);
    }

    [Fact]
    public async Task should_not_process_if_morning_checkin_not_completed()
    {
        // arrange
        var item1 = new CheckinItem(
            checkinFields: new CheckinFields("sheet", "2024-03-31", "Mar", "AG1"),
            formResponse: new Dictionary<string, string>()
            {
                ["Journal"] = "1" // missing "Feel Well-Rested" -> morning check-in not completed
            },
            getWeight: false);
        var queue = new List<CheckinItem> { item1 };
        var processor = SetupProcessor();

        // act
        var result = await processor.ProcessQueueAsync(queue, concatResults: false, forceProcessing: false, delimiter: null);

        // assert
        result.ShouldNotBeNull();
        result.Results.Count.ShouldBe(0);
        result.Unprocessed.Count.ShouldBe(1);
    }

    [Fact]
    public async Task should_not_process_if_getWeight_true_but_cannot_retrieve_weight_data()
    {
        // arrange
        var item1 = new CheckinItem(
            checkinFields: new CheckinFields("sheet", "2024-03-31", "Mar", "AG1"),
            formResponse: new Dictionary<string, string>()
            {
                ["Feel Well-Rested"] = "4"
            },
            getWeight: true);
        var queue = new List<CheckinItem> { item1 };
        var processor = SetupProcessor();

        // act
        var result = await processor.ProcessQueueAsync(queue, concatResults: false, forceProcessing: false, delimiter: null);

        // assert
        result.ShouldNotBeNull();
        result.Results.Count.ShouldBe(0);
        result.Unprocessed.Count.ShouldBe(1);
    }

    [Fact]
    public async Task should_not_process_if_tracked_activities_in_queue_and_cannot_retrieve_activity_data()
    {
        // arrange
        var item1 = new CheckinItem(
            checkinFields: new CheckinFields("sheet", "2024-03-31", "Mar", "AG1"),
            formResponse: new Dictionary<string, string>()
            {
                ["Hike"] = "1",
                ["Feel Well-Rested"] = "4"
            },
            getWeight: false);
        var queue = new List<CheckinItem> { item1 };
        var processor = SetupProcessor();

        // act
        var result = await processor.ProcessQueueAsync(queue, concatResults: false, forceProcessing: false, delimiter: null);

        // assert
        result.ShouldNotBeNull();
        result.Results.Count.ShouldBe(0);
        result.Unprocessed.Count.ShouldBe(1);
    }
}

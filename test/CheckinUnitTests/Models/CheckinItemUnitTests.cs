using CheckinApi.Models;

namespace CheckinUnitTests.Models;

public class CheckinItemUnitTests
{
    [Fact]
    public void should_calculate_total_time_in_bed_and_add_to_form_response()
    {
        // arrange
        var item = new CheckinItem(
            checkinFields: new CheckinFields("sheet", "2024-03-29", "Mar", "AE1"),
            formResponse: new Dictionary<string, string>() { ["Bedtime"] = "9:14:00 PM", ["Wake-up time"] = "4:57:00 PM" },
            getWeight: false,
            sleepStart: 1709954057,
            sleepEnd: 1709981828);

        // act
        item.UpdateTimeInBed();

        // assert
        item.FormResponse.Keys.Count.ShouldBe(3);
        item.FormResponse["Total Time in Bed"].ShouldBe("7:42");
    }

    [Theory]
    [InlineData(1709954057L, null)]
    [InlineData(null, 1709981828L)]
    public void should_not_include_total_time_in_bed_if_sleepStart_or_sleepEnd_missing(long? sleepStart, long? sleepEnd)
    {
        // arrange
        var item = new CheckinItem(
            checkinFields: new CheckinFields("sheet", "2024-03-29", "Mar", "AE1"),
            formResponse: new Dictionary<string, string>() { ["Bedtime"] = "9:14:00 PM", ["Wake-up time"] = "4:57:00 PM" },
            getWeight: false,
            sleepStart,
            sleepEnd);

        // act
        item.UpdateTimeInBed();

        // assert
        item.FormResponse.Keys.Count.ShouldBe(2);
        item.FormResponse.ShouldNotContainKey("Total Time in Bed");
    }

    [Fact]
    public void should_add_weight_data_to_form_response_if_getWeight_true_and_weight_data_available_for_date()
    {
        // arrange
        var item = new CheckinItem(
            checkinFields: new CheckinFields("sheet", "2024-03-29", "Mar", "AE1"),
            formResponse: new Dictionary<string, string>() { ["Item 1"] = "1", ["Item 2"] = "1" },
            getWeight: true);
        var weight = new List<Weight>() { new Weight("2024-03-29", 24.5d, 17.2d, 85.6d) };

        // act
        item.UpdateWeightData(weight);

        // assert
        item.FormResponse.Keys.Count.ShouldBe(5);
        item.FormResponse["BMI"].ShouldBe("24.5");
        item.FormResponse["Body fat %"].ShouldBe("17.2");
        item.FormResponse["Weight (lbs)"].ShouldBe("188.7");
    }

    [Fact]
    public void should_not_add_weight_data_to_form_response_if_no_weight_data_available_for_date()
    {
        // arrange
        var item = new CheckinItem(
            checkinFields: new CheckinFields("sheet", "2024-03-29", "Mar", "AE1"),
            formResponse: new Dictionary<string, string>() { ["Item 1"] = "1", ["Item 2"] = "1" },
            getWeight: true);
        // var weight = new List<Weight>();
        var weight = new List<Weight>() { new Weight("2024-03-01", 24.5d, 17.2d, 85.6d) };

        // act
        item.UpdateWeightData(weight);

        // assert
        item.FormResponse.Keys.Count.ShouldBe(2);
        item.FormResponse.ShouldNotContainKey("BMI");
        item.FormResponse.ShouldNotContainKey("Body fat %");
        item.FormResponse.ShouldNotContainKey("Weight (lbs)");
    }

    [Fact]
    public void should_not_add_weight_data_to_form_response_if_getWeight_false()
    {
        // arrange
        var item = new CheckinItem(
            checkinFields: new CheckinFields("sheet", "2024-03-29", "Mar", "AE1"),
            formResponse: new Dictionary<string, string>() { ["Item 1"] = "1", ["Item 2"] = "1" },
            getWeight: false);
        var weight = new List<Weight>() { new Weight("2024-03-29", 24.5d, 17.2d, 85.6d) };

        // act
        item.UpdateWeightData(weight);

        // assert
        item.FormResponse.Keys.Count.ShouldBe(2);
        item.FormResponse.ShouldNotContainKey("BMI");
        item.FormResponse.ShouldNotContainKey("Body fat %");
        item.FormResponse.ShouldNotContainKey("Weight (lbs)");
    }

    [Fact]
    public void should_not_add_activity_data_to_form_response_if_not_available_for_date()
    {
        // arrange
        var item = new CheckinItem(
            checkinFields: new CheckinFields("sheet", "2024-03-16", "Mar", "R1"),
            formResponse: new Dictionary<string, string>() { ["Item 1"] = "1", ["Item 2"] = "1" },
            getWeight: false);
        var trackedActivities = new List<string>() { "Hike", "Kayaking", "Ride", "Run" };
        var activityData = new List<StravaActivity>();

        // act
        item.ProcessActivityData(activityData, trackedActivities);

        // assert
        item.FormResponse.Keys.Count.ShouldBe(2);
        item.FormResponse.Keys.Any(key => trackedActivities.Contains(key)).ShouldBeFalse();
    }

    [Fact]
    public void should_add_activity_data_to_form_response_if_available_for_date()
    {
        // arrange
        var item = new CheckinItem(
            checkinFields: new CheckinFields("sheet", "2024-03-16", "Mar", "R1"),
            formResponse: new Dictionary<string, string>() { ["Item 1"] = "1", ["Item 2"] = "1" },
            getWeight: false);
        var trackedActivities = new List<string>() { "Hike", "Kayaking", "Ride", "Run" };
        var activityData = new List<StravaActivity>()
        {
            new StravaActivity("3/16", "Hike", 13840.4d, "2024-03-16T17:11:48Z", "2024-03-16T12:11:48Z")
        };

        // act
        item.ProcessActivityData(activityData, trackedActivities);

        // assert
        item.FormResponse.Keys.Count.ShouldBe(3);
        item.FormResponse.ShouldContainKeyAndValue("Hike", "8.6");
    }

    [Fact]
    public void should_sum_total_mileage_for_activity_if_performed_multiple_times_on_same_day()
    {
        // arrange
        var item = new CheckinItem(
            checkinFields: new CheckinFields("sheet", "2024-03-16", "Mar", "R1"),
            formResponse: new Dictionary<string, string>() { ["Item 1"] = "1", ["Item 2"] = "1" },
            getWeight: false);
        var trackedActivities = new List<string>() { "Hike", "Kayaking", "Ride", "Run" };
        var activityData = new List<StravaActivity>()
        {
            new StravaActivity("3/16", "Hike", 13840.4d, "2024-03-16T17:11:48Z", "2024-03-16T12:11:48Z"),
            new StravaActivity("3/16 round 2", "Hike", 4023.4d, "2024-03-16T22:11:48Z", "2024-03-16T17:11:48Z")
        };

        // act
        item.ProcessActivityData(activityData, trackedActivities);

        // assert
        item.FormResponse.Keys.Count.ShouldBe(3);
        item.FormResponse.ShouldContainKeyAndValue("Hike", "11.1");
    }

    [Fact]
    public void should_add_activity_data_for_multiple_activity_types_to_form_response()
    {
        // arrange
        var item = new CheckinItem(
            checkinFields: new CheckinFields("sheet", "2024-03-16", "Mar", "R1"),
            formResponse: new Dictionary<string, string>() { ["Item 1"] = "1", ["Item 2"] = "1" },
            getWeight: false);
        var trackedActivities = new List<string>() { "Hike", "Kayaking", "Ride", "Run" };
        var activityData = new List<StravaActivity>()
        {
            new StravaActivity("3/16 Hike", "Hike", 13840.4d, "2024-03-16T17:11:48Z", "2024-03-16T12:11:48Z"),
            new StravaActivity("3/16 Ride", "Ride", 40716.4d, "2024-03-16T22:11:48Z", "2024-03-16T17:11:48Z")
        };

        // act
        item.ProcessActivityData(activityData, trackedActivities);

        // assert
        item.FormResponse.Keys.Count.ShouldBe(4);
        item.FormResponse.ShouldContainKeyAndValue("Hike", "8.6");
        item.FormResponse.ShouldContainKeyAndValue("Ride", "25.3");
    }

    [Fact]
    public void should_build_results_string_prefixed_with_checkin_date()
    {
        // arrange
        var item = new CheckinItem(
            checkinFields: new CheckinFields("sheet", "2024-03-29", "Mar", "AE1"),
            formResponse: new Dictionary<string, string>() { ["Item 1"] = "1", ["Item 2"] = "1", ["Item 3"] = "1" },
            getWeight: false);

        var fullChecklist = new List<string>() { "Item 1", "Item 2", "Item 3" };

        // act
        var result = item.BuildResultsString(fullChecklist);

        // assert
        result.ShouldNotBeNull();
        result.ShouldBe("29,1,1,1");
    }

    [Fact]
    public void should_include_comma_with_no_value_for_items_not_in_form_response()
    {
        // arrange
        var item = new CheckinItem(
            checkinFields: new CheckinFields("sheet", "2024-03-29", "Mar", "AE1"),
            formResponse: new Dictionary<string, string>() { ["Item 1"] = "1", ["Item 3"] = "1", ["Item 5"] = "1" },
            getWeight: false);

        var fullChecklist = new List<string>() { "Item 1", "Item 2", "Item 3", "Item 4", "Item 5" };

        // act
        var result = item.BuildResultsString(fullChecklist);

        // assert
        result.ShouldNotBeNull();
        result.ShouldBe("29,1,,1,,1");
    }
}

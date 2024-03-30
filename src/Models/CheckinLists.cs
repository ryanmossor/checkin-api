using System.Text.Json.Serialization;

namespace CheckinApi.Models;

public class CheckinLists
{
    public List<string> FullChecklist { get; private set; }
    public List<string> TrackedActivities { get; private set; }

    [JsonConstructor]
    public CheckinLists(List<string> fullChecklist, List<string> trackedActivities)
    {
        FullChecklist = fullChecklist;
        TrackedActivities = trackedActivities;
    }
}

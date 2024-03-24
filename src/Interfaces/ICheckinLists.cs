using CheckinApi.Services;

namespace CheckinApi.Interfaces;

public interface ICheckinLists
{
    List<string> FullChecklist { get; }
    List<string> TrackedActivities { get; }

    Task<CheckinLists> UpdateListsAsync(CheckinLists request);
}

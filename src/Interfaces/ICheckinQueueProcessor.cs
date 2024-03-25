using CheckinApi.Models;

namespace CheckinApi.Interfaces;

public interface ICheckinQueueProcessor
{
    Task<CheckinResponse> ProcessSavedResultsAsync(string dates);
    Task<CheckinResponse> ProcessQueueAsync(List<CheckinItem> queue);
}

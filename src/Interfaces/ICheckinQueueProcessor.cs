using CheckinApi.Models;

namespace CheckinApi.Interfaces;

public interface ICheckinQueueProcessor
{
    Task<CheckinResponse> ProcessSavedResultsAsync(string dates, bool concatResults, string? delimiter);
    Task<CheckinResponse> ProcessQueueAsync(List<CheckinItem> queue, bool concatResults, bool forceProcessing, string? delimiter);
}

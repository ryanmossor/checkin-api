using CheckinApi.Models;

namespace CheckinApi.Interfaces;

public interface ICheckinQueueProcessor
{
    Task<CheckinResponse> ProcessAsync(List<CheckinItem> queue);
}
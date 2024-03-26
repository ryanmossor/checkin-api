using CheckinApi.Models;

namespace CheckinApi.Interfaces;

public interface IHealthTrackingService
{
    Task<List<Weight>> GetWeightDataAsync(List<CheckinItem> queue);
}

using CheckinApi.Models;

namespace CheckinApi.Interfaces;

public interface IHealthTrackingService
{
    Task<Weight[]?> GetWeightDataAsync(List<CheckinItem> queue);
}

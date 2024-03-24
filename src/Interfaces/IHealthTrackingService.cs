using CheckinApi.Models;

namespace CheckinApi.Interfaces;

public interface IHealthTrackingService
{
    Task<WeightData?> GetWeightDataAsync(string date);
}
using CheckinApi.Models;

namespace CheckinApi.Interfaces;

public interface IActivityService
{
    Task<List<StravaActivity>> GetActivityDataAsync(List<CheckinItem> queue);
}

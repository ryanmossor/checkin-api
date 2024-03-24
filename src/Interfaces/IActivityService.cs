using CheckinApi.Models;

namespace CheckinApi.Interfaces;

public interface IActivityService
{
    Task<StravaActivity[]?> GetActivityDataAsync(List<CheckinItem> queue);
}

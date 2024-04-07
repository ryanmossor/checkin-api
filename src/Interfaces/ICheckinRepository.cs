using CheckinApi.Config;
using CheckinApi.Models;

namespace CheckinApi.Interfaces;

public interface ICheckinRepository
{
    Task UpdateCheckinSecretsAsync(CheckinSecrets secrets);

    Task SaveCheckinRequestAsync(List<CheckinItem> request);

    Task SaveCheckinItemAsync(CheckinItem checkinItem);

    List<string?> GetAllCheckinDates();

    Task<CheckinItem?> GetCheckinItemAsync(string date);
    Task<List<CheckinItem>> GetCheckinItemsAsync(List<string> dates);

    Task<CheckinLists?> GetCheckinListsAsync();
    Task<CheckinLists> UpdateCheckinListsAsync(CheckinLists lists);
}

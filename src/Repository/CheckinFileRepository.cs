using CheckinApi.Config;
using CheckinApi.Extensions;
using CheckinApi.Interfaces;
using CheckinApi.Models;

namespace CheckinApi.Repository;

public class CheckinFileRepository : ICheckinRepository
{
    private readonly CheckinConfig _config;
    private readonly ILogger<CheckinFileRepository> _logger;

    public CheckinFileRepository(CheckinConfig config, ILogger<CheckinFileRepository> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task UpdateCheckinSecretsAsync(CheckinSecrets secrets)
    {
        try
        {
            await File.WriteAllTextAsync(_config.SecretsFile, secrets.SerializePretty());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating check-in secrets");
        }
    }

    public async Task SaveCheckinRequestAsync(CheckinRequest request)
    {
        try
        {
            var json = request.Serialize();
            var filename = $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.json";
            await File.WriteAllTextAsync(Path.Combine(_config.RequestsDir, filename), json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving check-in request {@request}", request);
        }
    }

    public List<string?> GetAllCheckinDates()
    {
        try
        {
            var files = Directory.GetFiles(_config.ResultsDir).Select(Path.GetFileNameWithoutExtension).ToList();
            return files;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting list of check-in results");
        }

        return new List<string?>();
    }

    public async Task SaveCheckinItemAsync(CheckinItem checkinItem)
    {
        try
        {
            var json = checkinItem.Serialize();
            await File.WriteAllTextAsync(Path.Combine(_config.ResultsDir, $"{checkinItem.CheckinFields.Date}.json"), json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error writing check-in results to file");
        }
    }

    public async Task<CheckinItem?> GetCheckinItemAsync(string date)
    {
        try
        {
            var contents = await File.ReadAllTextAsync(Path.Combine(_config.ResultsDir, $"{date}.json"));
            return contents.Deserialize<CheckinItem>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting results for {date}", date);
        }

        return null;
    }

    public async Task<List<CheckinItem>> GetCheckinItemsAsync(List<string> dates)
    {
        var results = new List<CheckinItem>();
        foreach (var date in dates)
        {
            var item = await GetCheckinItemAsync(date);
            if (item != null)
            {
                results.Add(item);
            }
        }

        return results;
    }

    public async Task<CheckinLists?> GetCheckinListsAsync()
    {
        try
        {
            var listsJson = await File.ReadAllTextAsync(_config.ChecklistsFile);
            return listsJson.Deserialize<CheckinLists>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting check-in lists");
        }

        return null;
    }

    public async Task<CheckinLists> UpdateCheckinListsAsync(CheckinLists lists)
    {
        try
        {
            var json = lists.Serialize();
            await File.WriteAllTextAsync(_config.ChecklistsFile, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating check-in lists");
        }

        return lists;
    }
}

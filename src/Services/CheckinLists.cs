using System.Text.Json.Serialization;
using CheckinApi.Extensions;
using CheckinApi.Interfaces;
using Constants = CheckinApi.Models.Constants;

namespace CheckinApi.Services;

public class CheckinLists : ICheckinLists
{
    private readonly object _lock = new();
    private readonly ILogger<CheckinLists> _logger;

    public List<string>? FullChecklist { get; private set; }
    public List<string>? TrackedActivities { get; private set; }
    
    public CheckinLists(ILogger<CheckinLists> logger)
    {
        _logger = logger;
        
        try 
        {
            var listsJson = File.ReadAllText(Constants.ListsFile);
            var lists = listsJson.Deserialize<CheckinLists>();
            
            _logger.LogTrace("Initializing check-in lists: {lists}", listsJson);
            FullChecklist = lists.FullChecklist;
            TrackedActivities = lists.TrackedActivities;
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Error reading lists.json");
        }
    }

    [JsonConstructor]
    public CheckinLists(List<string> fullChecklist, List<string> trackedActivities)
    {
        FullChecklist = fullChecklist;
        TrackedActivities = trackedActivities;
    }

    public async Task<CheckinLists> UpdateListsAsync(CheckinLists request)
    {
        lock (_lock)
        {
            FullChecklist = request.FullChecklist ?? FullChecklist;
            TrackedActivities = request.TrackedActivities ?? TrackedActivities;
        }

        try
        {
            var json = this.SerializeFlat().Replace("\\u003C", "<");
            await File.WriteAllTextAsync(Constants.ListsFile, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating lists.json");
        }

        return this;
    }
}

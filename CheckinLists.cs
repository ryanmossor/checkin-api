using System.Text.Json.Serialization;
using CheckinApi.Extensions;

namespace CheckinApi;

public class CheckinLists : ICheckinLists
{
    private readonly object _lock = new();
    private readonly ILogger<CheckinLists> _logger;

    public List<string> FullChecklist { get; private set; }
    public List<string> TrackedActivities { get; private set; }
    
    public CheckinLists(ILogger<CheckinLists> logger)
    {
        _logger = logger;
        
        try 
        {
            var listsJson = File.ReadAllText("./data/lists.json");
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

    public CheckinLists GetLists() => this;
    
    public async Task<CheckinLists> UpdateLists(List<string> fullChecklist, List<string> trackedActivities)
    {
        lock (_lock)
        {
            FullChecklist = fullChecklist;
            TrackedActivities = trackedActivities;
        }

        try
        {
            var json = this.Serialize().Replace("\\u003C", "<");
            await File.WriteAllTextAsync("./data/lists.json", json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating lists.json");
        }

        return this;
    }
}

public interface ICheckinLists
{
    List<string> FullChecklist { get; }
    List<string> TrackedActivities { get; }

    CheckinLists GetLists();
    Task<CheckinLists> UpdateLists(List<string> fullChecklist, List<string> trackedActivities);
}
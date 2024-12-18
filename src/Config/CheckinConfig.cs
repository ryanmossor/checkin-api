namespace CheckinApi.Config;

public class CheckinConfig
{
    public string DataDir { get; init; } = null!;
    public string ChecklistsFile { get; init; } = null!;
    public string SecretsFile { get; init; } = null!;
    public string RequestsDir { get; init; } = null!;
    public string ResultsDir { get; init; } = null!;
    public string EditedResultsDir { get; init; } = null!;
}

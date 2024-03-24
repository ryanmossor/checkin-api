namespace CheckinApi.Models;

public static class Constants
{
    public static string DataDir { get; set; } = "/app/data";
    
    public static string ListsFile => Path.Combine(DataDir, "lists.json");
    public static string SecretsFile => Path.Combine(DataDir, "secrets.json");
    public static string RequestsDir => Path.Combine(DataDir, "requests");
    public static string ResultsDir => Path.Combine(DataDir, "results");
}

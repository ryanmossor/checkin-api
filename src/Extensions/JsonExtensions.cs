using System.Text.Encodings.Web;
using System.Text.Json;

namespace CheckinApi.Extensions;

public static class JsonExtensions
{
    private static readonly JsonSerializerOptions DefaultSerializerSettings = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static T Deserialize<T>(this string json) => JsonSerializer.Deserialize<T>(json, DefaultSerializerSettings);

    public static string Serialize<T>(this T obj) => JsonSerializer.Serialize(obj, DefaultSerializerSettings);

    public static string SerializePretty<T>(this T obj)
    {
        var opts = new JsonSerializerOptions(DefaultSerializerSettings)
        {
            WriteIndented = true,
        };
        return JsonSerializer.Serialize(obj, opts);
    }
}

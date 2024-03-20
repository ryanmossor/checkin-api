using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace CheckinApi.Extensions;

public static class JsonExtensions
{
    private static readonly JsonSerializerOptions DefaultSerializerSettings = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.BasicLatin),
        WriteIndented = true,
    };

    public static T Deserialize<T>(this string json) => JsonSerializer.Deserialize<T>(json, DefaultSerializerSettings);

    public static string Serialize<T>(this T obj) => JsonSerializer.Serialize(obj, DefaultSerializerSettings);
}
using System.Text.Json.Serialization;

namespace CheckinApi.Config;

public class StravaTokenInfo
{
    public string token_type { get; }
    public string access_token { get; }
    public long expires_at { get; }
    public int expires_in { get; }
    public string refresh_token { get; }
    
    [JsonConstructor]
    public StravaTokenInfo(string token_type, string access_token, long expires_at, int expires_in, string refresh_token)
    {
        this.token_type = token_type;
        this.access_token = access_token;
        this.expires_at = expires_at;
        this.expires_in = expires_in;
        this.refresh_token = refresh_token;
    }
}

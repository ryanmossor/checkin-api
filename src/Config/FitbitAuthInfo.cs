using System.Text.Json.Serialization;

namespace CheckinApi.Config;

public class FitbitAuthInfo
{
    public string access_token { get; private set; }
    public int expires_in { get; private set; }
    public string refresh_token { get; private set; }
    public string scope { get; private set; }
    public string token_type { get; private set; }
    public string user_id { get; private set; }

    [JsonConstructor]
    public FitbitAuthInfo(string access_token, int expires_in, string refresh_token, string scope, string token_type, string user_id)
    {
        this.access_token = access_token;
        this.expires_in = expires_in;
        this.refresh_token = refresh_token;
        this.scope = scope;
        this.token_type = token_type;
        this.user_id = user_id;
    }
}

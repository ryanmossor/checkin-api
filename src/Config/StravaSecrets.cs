using System.Text.Json.Serialization;

namespace CheckinApi.Config;

public class StravaSecrets
{
    public int client_id { get; private set; }
    public string client_secret { get; private set; }
    public StravaAuthInfo auth { get; private set; }

    [JsonConstructor]
    public StravaSecrets(int client_id, string client_secret, StravaAuthInfo auth)
    {
        this.client_id = client_id;
        this.client_secret = client_secret;
        this.auth = auth;
    }

    public void UpdateAuth(StravaAuthInfo refreshedAuthData) => auth = refreshedAuthData;
}

using System.Text.Json.Serialization;

namespace CheckinApi.Config;

public class FitbitSecrets
{
    public string client_id { get; private set; }
    public string client_secret { get; private set; }
    public FitbitAuthInfo auth { get; private set; }

    [JsonConstructor]
    public FitbitSecrets(string client_id, string client_secret, FitbitAuthInfo auth)
    {
        this.client_id = client_id;
        this.client_secret = client_secret;
        this.auth = auth;
    }

    public void UpdateAuth(FitbitAuthInfo refreshedAuthData) => auth = refreshedAuthData;
}

namespace CheckinApi.Config;

public class CheckinSecrets
{
    public StravaSecrets Strava { get; }
    public FitbitSecrets Fitbit { get; }

    public CheckinSecrets(StravaSecrets strava, FitbitSecrets fitbit)
    {
        Strava = strava;
        Fitbit = fitbit;
    }
}
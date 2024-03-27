using CheckinApi.Config;

namespace CheckinApi.Interfaces;

public interface IAuthService
{
    Task RefreshTokenAsync();
    bool IsTokenExpired();
}

public interface IFitbitAuthService : IAuthService
{
    FitbitAuthInfo Auth { get; }
}

public interface IStravaAuthService : IAuthService
{
    StravaAuthInfo Auth { get; }
}

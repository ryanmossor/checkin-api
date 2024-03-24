namespace CheckinApi.Interfaces;

public interface IAuthService
{
    Task RefreshTokenAsync();
    bool IsTokenExpired();
}

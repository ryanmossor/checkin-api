namespace CheckinApi.Interfaces;

public interface ITokenService
{
    Task RefreshTokenAsync();
    bool IsTokenExpired();
}
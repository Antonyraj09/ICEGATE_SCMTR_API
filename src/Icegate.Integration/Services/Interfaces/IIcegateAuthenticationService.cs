namespace Icegate.Integration.Services.Interfaces;

/// <summary>Calls the ICEGATE Authentication API and returns a freshly generated token + its expiry.</summary>
public interface IIcegateAuthenticationService
{
    Task<(string Token, DateTimeOffset ExpiresAtUtc)> AuthenticateAsync(CancellationToken cancellationToken = default);
}

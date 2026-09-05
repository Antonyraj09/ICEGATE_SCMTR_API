namespace Icegate.Integration.Services.Interfaces;

/// <summary>Calls the ICEGATE Authentication API for a given client's credentials and returns a freshly generated token + its expiry.</summary>
public interface IIcegateAuthenticationService
{
    Task<(string Token, DateTimeOffset ExpiresAtUtc)> AuthenticateAsync(string clientId, CancellationToken cancellationToken = default);
}

namespace Icegate.Integration.Services.Interfaces;

/// <summary>
/// Centralized ICEGATE token lifecycle manager, keyed per client (each onboarded client has
/// its own ICEGATE identity and therefore its own token). Generates a token only when no
/// valid one exists for that client, reuses it across requests, and refreshes it before its
/// ~15 minute expiry using a configurable safety buffer. Token values are never logged and
/// never exposed to any client's legacy application.
/// </summary>
public interface IIcegateTokenService
{
    /// <summary>Returns a currently-valid ICEGATE token for the given client, generating or refreshing it as needed.</summary>
    Task<string> GetTokenAsync(string clientId, CancellationToken cancellationToken = default);

    /// <summary>Clears the cached token for the given client, forcing the next <see cref="GetTokenAsync"/> call to generate a new one.</summary>
    void InvalidateCachedToken(string clientId);
}

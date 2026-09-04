namespace Icegate.Integration.Services.Interfaces;

/// <summary>
/// Centralized ICEGATE token lifecycle manager. Generates a token only when no valid one
/// exists, reuses it across requests, and refreshes it before its ~15 minute expiry using a
/// configurable safety buffer. The token value is never logged and never exposed to the
/// legacy application.
/// </summary>
public interface IIcegateTokenService
{
    /// <summary>Returns a currently-valid ICEGATE token, generating or refreshing it as needed.</summary>
    Task<string> GetTokenAsync(CancellationToken cancellationToken = default);

    /// <summary>Clears any cached token, forcing the next <see cref="GetTokenAsync"/> call to generate a new one.</summary>
    void InvalidateCachedToken();
}

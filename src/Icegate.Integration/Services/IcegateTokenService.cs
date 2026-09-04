using Icegate.Integration.Configuration;
using Icegate.Integration.Models.Authentication;
using Icegate.Integration.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace Icegate.Integration.Services;

/// <summary>
/// Centralized in-memory ICEGATE token manager. Generates a token only when no valid
/// cached token exists, reuses it across requests, and refreshes it automatically before
/// the configurable safety buffer expires. Thread-safe via a semaphore so concurrent
/// requests do not each generate a new token. The token value is never logged.
/// </summary>
public class IcegateTokenService : IIcegateTokenService
{
    private readonly IIcegateAuthenticationService _authenticationService;
    private readonly IOptionsMonitor<IcegateSettings> _settings;
    private readonly ILogger<IcegateTokenService> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private IcegateCachedToken? _cachedToken;

    public IcegateTokenService(
        IIcegateAuthenticationService authenticationService,
        IOptionsMonitor<IcegateSettings> settings,
        ILogger<IcegateTokenService> logger)
    {
        _authenticationService = authenticationService;
        _settings = settings;
        _logger = logger;
    }

    public async Task<string> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        var safetyBuffer = TimeSpan.FromSeconds(Math.Max(0, _settings.CurrentValue.TokenSafetyBufferSeconds));

        var existing = _cachedToken;
        if (existing is not null && existing.IsUsable(safetyBuffer))
        {
            return existing.Token;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            // Re-check after acquiring the lock: another caller may have already refreshed it.
            existing = _cachedToken;
            if (existing is not null && existing.IsUsable(safetyBuffer))
            {
                return existing.Token;
            }

            return await GenerateAndCacheTokenAsync(cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public void InvalidateCachedToken()
    {
        _logger.LogInformation("Invalidating cached ICEGATE token.");
        _cachedToken = null;
    }

    private async Task<string> GenerateAndCacheTokenAsync(CancellationToken cancellationToken)
    {
        var (token, expiresAtUtc) = await _authenticationService.AuthenticateAsync(cancellationToken);

        _cachedToken = new IcegateCachedToken
        {
            Token = token,
            IssuedAtUtc = DateTimeOffset.UtcNow,
            ExpiresAtUtc = expiresAtUtc
        };

        _logger.LogInformation("ICEGATE token cached. ExpiresAtUtc={ExpiresAtUtc}", expiresAtUtc);

        return token;
    }
}

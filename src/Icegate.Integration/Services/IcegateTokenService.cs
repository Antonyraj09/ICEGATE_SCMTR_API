using System.Collections.Concurrent;
using Icegate.Integration.Configuration;
using Icegate.Integration.Models.Authentication;
using Icegate.Integration.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace Icegate.Integration.Services;

/// <summary>
/// Centralized in-memory ICEGATE token manager, keyed per client. Generates a token only
/// when no valid cached token exists for that client, reuses it across requests, and
/// refreshes it automatically before the configurable safety buffer expires. Thread-safe
/// per-client via a per-client semaphore, so concurrent requests for the SAME client do not
/// each generate a new token, while different clients never block on each other. Token
/// values are never logged.
/// </summary>
public class IcegateTokenService : IIcegateTokenService
{
    private readonly IIcegateAuthenticationService _authenticationService;
    private readonly IOptionsMonitor<IcegateSettings> _settings;
    private readonly ILogger<IcegateTokenService> _logger;

    private readonly ConcurrentDictionary<string, IcegateCachedToken> _cachedTokensByClient = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locksByClient = new(StringComparer.OrdinalIgnoreCase);

    public IcegateTokenService(
        IIcegateAuthenticationService authenticationService,
        IOptionsMonitor<IcegateSettings> settings,
        ILogger<IcegateTokenService> logger)
    {
        _authenticationService = authenticationService;
        _settings = settings;
        _logger = logger;
    }

    public async Task<string> GetTokenAsync(string clientId, CancellationToken cancellationToken = default)
    {
        var safetyBuffer = TimeSpan.FromSeconds(Math.Max(0, _settings.CurrentValue.TokenSafetyBufferSeconds));

        if (_cachedTokensByClient.TryGetValue(clientId, out var existing) && existing.IsUsable(safetyBuffer))
        {
            return existing.Token;
        }

        var clientLock = _locksByClient.GetOrAdd(clientId, _ => new SemaphoreSlim(1, 1));

        await clientLock.WaitAsync(cancellationToken);
        try
        {
            // Re-check after acquiring the lock: another caller for the same client may have
            // already refreshed it while we were waiting.
            if (_cachedTokensByClient.TryGetValue(clientId, out existing) && existing.IsUsable(safetyBuffer))
            {
                return existing.Token;
            }

            return await GenerateAndCacheTokenAsync(clientId, cancellationToken);
        }
        finally
        {
            clientLock.Release();
        }
    }

    public void InvalidateCachedToken(string clientId)
    {
        _logger.LogInformation("Invalidating cached ICEGATE token for ClientId={ClientId}.", clientId);
        _cachedTokensByClient.TryRemove(clientId, out _);
    }

    private async Task<string> GenerateAndCacheTokenAsync(string clientId, CancellationToken cancellationToken)
    {
        var (token, expiresAtUtc) = await _authenticationService.AuthenticateAsync(clientId, cancellationToken);

        _cachedTokensByClient[clientId] = new IcegateCachedToken
        {
            Token = token,
            IssuedAtUtc = DateTimeOffset.UtcNow,
            ExpiresAtUtc = expiresAtUtc
        };

        _logger.LogInformation("ICEGATE token cached for ClientId={ClientId}. ExpiresAtUtc={ExpiresAtUtc}", clientId, expiresAtUtc);

        return token;
    }
}

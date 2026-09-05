using System.Net.Http.Json;
using Icegate.Integration.Configuration;
using Icegate.Integration.Http;
using Icegate.Integration.Models.Authentication;
using Icegate.Integration.Models.Common;
using Icegate.Integration.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace Icegate.Integration.Services;

/// <summary>
/// Calls the ICEGATE Authentication API (POST {AuthenticationUrl}, application/json) using the
/// requesting client's own encrypted credential payload - never plaintext username/password,
/// and never another client's credentials. The Authentication URL/environment/timeout are
/// shared infrastructure (<see cref="IcegateSettings"/>); the credential itself is per-client
/// (<see cref="IcegateClientSettings"/>, resolved via <see cref="IIcegateClientRegistry"/>).
/// </summary>
public class IcegateAuthenticationService : IIcegateAuthenticationService
{
    private readonly IIcegateHttpClient _httpClient;
    private readonly IIcegateClientRegistry _clientRegistry;
    private readonly IOptionsMonitor<IcegateSettings> _settings;
    private readonly ILogger<IcegateAuthenticationService> _logger;

    public IcegateAuthenticationService(
        IIcegateHttpClient httpClient,
        IIcegateClientRegistry clientRegistry,
        IOptionsMonitor<IcegateSettings> settings,
        ILogger<IcegateAuthenticationService> logger)
    {
        _httpClient = httpClient;
        _clientRegistry = clientRegistry;
        _settings = settings;
        _logger = logger;
    }

    public async Task<(string Token, DateTimeOffset ExpiresAtUtc)> AuthenticateAsync(string clientId, CancellationToken cancellationToken = default)
    {
        var settings = _settings.CurrentValue;

        if (string.IsNullOrWhiteSpace(settings.AuthenticationUrl))
        {
            throw new IcegateEndpointNotConfirmedException(nameof(settings.AuthenticationUrl));
        }

        if (!_clientRegistry.TryGetByClientId(clientId, out var client) || client is null)
        {
            throw new IcegateTokenException(IcegateErrorCode.InvalidApiKey, $"No enabled client is registered for ClientId '{clientId}'.");
        }

        if (string.IsNullOrWhiteSpace(client.EncryptedCredentialData))
        {
            throw new IcegateTokenException(
                IcegateErrorCode.InvalidApiKey,
                $"IcegateClients[{clientId}].EncryptedCredentialData is not configured. Configure the encrypted credential payload via secret storage.");
        }

        var request = new IcegateAuthenticationRequest { Data = client.EncryptedCredentialData };

        _logger.LogInformation("Requesting a new ICEGATE token. ClientId={ClientId} Environment={Environment}", clientId, settings.Environment);

        using var response = await _httpClient.PostJsonAsync(settings.AuthenticationUrl, request, cancellationToken: cancellationToken);
        await response.EnsureIcegateSuccessAsync(cancellationToken);

        var body = await response.Content.ReadFromJsonAsync<IcegateAuthenticationResponse>(cancellationToken: cancellationToken)
            ?? throw new IcegateTokenException(IcegateErrorCode.TokenInvalidOrExpired, "ICEGATE returned an empty authentication response.");

        var token = body.ResolveToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new IcegateTokenException(IcegateErrorCode.TokenInvalidOrExpired, "ICEGATE authentication response did not contain a token.");
        }

        var lifetimeSeconds = body.ExpiresInSeconds is > 0 ? body.ExpiresInSeconds.Value : settings.TokenNominalLifetimeSeconds;
        var expiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(lifetimeSeconds);

        _logger.LogInformation("ICEGATE token generated successfully. ClientId={ClientId} ExpiresAtUtc={ExpiresAtUtc}", clientId, expiresAtUtc);

        // Never log the token value itself.
        return (token, expiresAtUtc);
    }
}

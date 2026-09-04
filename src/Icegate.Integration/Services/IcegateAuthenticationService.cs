using System.Net.Http.Json;
using Icegate.Integration.Configuration;
using Icegate.Integration.Http;
using Icegate.Integration.Models.Authentication;
using Icegate.Integration.Models.Common;
using Icegate.Integration.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace Icegate.Integration.Services;

/// <summary>
/// Calls the ICEGATE Authentication API (POST {AuthenticationUrl}, application/json).
/// Sends only the pre-built encrypted credential payload - never plaintext username/password.
/// </summary>
public class IcegateAuthenticationService : IIcegateAuthenticationService
{
    private readonly IIcegateHttpClient _httpClient;
    private readonly IOptionsMonitor<IcegateSettings> _settings;
    private readonly ILogger<IcegateAuthenticationService> _logger;

    public IcegateAuthenticationService(
        IIcegateHttpClient httpClient,
        IOptionsMonitor<IcegateSettings> settings,
        ILogger<IcegateAuthenticationService> logger)
    {
        _httpClient = httpClient;
        _settings = settings;
        _logger = logger;
    }

    public async Task<(string Token, DateTimeOffset ExpiresAtUtc)> AuthenticateAsync(CancellationToken cancellationToken = default)
    {
        var settings = _settings.CurrentValue;

        if (string.IsNullOrWhiteSpace(settings.AuthenticationUrl))
        {
            throw new IcegateEndpointNotConfirmedException(nameof(settings.AuthenticationUrl));
        }

        if (string.IsNullOrWhiteSpace(settings.EncryptedCredentialData))
        {
            throw new IcegateTokenException(
                IcegateErrorCode.InvalidApiKey,
                "ICEGATE:EncryptedCredentialData is not configured. Configure the encrypted credential payload via secret storage.");
        }

        var request = new IcegateAuthenticationRequest { Data = settings.EncryptedCredentialData };

        _logger.LogInformation("Requesting a new ICEGATE token (Environment={Environment}).", settings.Environment);

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

        _logger.LogInformation("ICEGATE token generated successfully. ExpiresAtUtc={ExpiresAtUtc}", expiresAtUtc);

        // Never log the token value itself.
        return (token, expiresAtUtc);
    }
}

using Icegate.Integration.Http;
using Icegate.Integration.Models.Common;
using Icegate.Integration.Services.Interfaces;

namespace Icegate.Integration.Services;

/// <inheritdoc cref="ITokenRetryExecutor"/>
public class TokenRetryExecutor : ITokenRetryExecutor
{
    private readonly IIcegateTokenService _tokenService;
    private readonly ILogger<TokenRetryExecutor> _logger;

    public TokenRetryExecutor(IIcegateTokenService tokenService, ILogger<TokenRetryExecutor> logger)
    {
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task<HttpResponseMessage> ExecuteAsync(
        string clientId,
        Func<string, CancellationToken, Task<HttpResponseMessage>> sendAsync,
        CancellationToken cancellationToken = default)
    {
        var token = await _tokenService.GetTokenAsync(clientId, cancellationToken);

        HttpResponseMessage response;
        try
        {
            response = await sendAsync(token, cancellationToken);
            await response.EnsureIcegateSuccessAsync(cancellationToken);
            return response;
        }
        catch (IcegateTokenException ex)
        {
            _logger.LogWarning(
                "ICEGATE token rejected for ClientId={ClientId} ({ErrorCode}). Clearing cached token and retrying once.",
                clientId, ex.ErrorCode);

            _tokenService.InvalidateCachedToken(clientId);
            var newToken = await _tokenService.GetTokenAsync(clientId, cancellationToken);

            response = await sendAsync(newToken, cancellationToken);
            await response.EnsureIcegateSuccessAsync(cancellationToken);
            return response;
        }
    }
}

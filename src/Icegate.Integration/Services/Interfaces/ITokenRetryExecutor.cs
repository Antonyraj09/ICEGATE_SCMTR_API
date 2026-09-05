namespace Icegate.Integration.Services.Interfaces;

/// <summary>
/// Executes an ICEGATE HTTP call for a given client, obtaining/reusing that client's token
/// from <see cref="IIcegateTokenService"/>. If ICEGATE responds that the token is
/// invalid/expired/missing, that client's cached token is cleared, a new one is generated,
/// and the ORIGINAL request is retried exactly once. Business validation failures and
/// transient errors are never retried by this executor - only token failures.
/// </summary>
public interface ITokenRetryExecutor
{
    Task<HttpResponseMessage> ExecuteAsync(
        string clientId,
        Func<string, CancellationToken, Task<HttpResponseMessage>> sendAsync,
        CancellationToken cancellationToken = default);
}

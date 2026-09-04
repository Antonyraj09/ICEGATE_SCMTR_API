using System.Net.Http.Json;
using Icegate.Integration.Helpers;
using Icegate.Integration.Models.Common;
using Microsoft.Extensions.Http;

namespace Icegate.Integration.Http;

public interface IIcegateHttpClient
{
    Task<HttpResponseMessage> PostJsonAsync(
        string url, object requestBody, IDictionary<string, string>? headers = null, CancellationToken cancellationToken = default);

    Task<HttpResponseMessage> PostMultipartAsync(
        string url, MultipartFormDataContent content, IDictionary<string, string>? headers = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Encapsulates all outbound HTTP communication with ICEGATE. Uses a named HttpClient
/// obtained from <see cref="IHttpClientFactory"/> (configured centrally in Program.cs with
/// timeout and a Polly transient-fault retry policy) rather than constructing HttpClient
/// instances per call.
/// </summary>
public class IcegateHttpClient : IIcegateHttpClient
{
    public const string HttpClientName = "IcegateClient";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<IcegateHttpClient> _logger;

    public IcegateHttpClient(IHttpClientFactory httpClientFactory, ILogger<IcegateHttpClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<HttpResponseMessage> PostJsonAsync(
        string url, object requestBody, IDictionary<string, string>? headers = null, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(requestBody)
        };

        ApplyHeaders(request, headers);
        return await SendAsync(request, cancellationToken);
    }

    public async Task<HttpResponseMessage> PostMultipartAsync(
        string url, MultipartFormDataContent content, IDictionary<string, string>? headers = null, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = content
        };

        ApplyHeaders(request, headers);
        return await SendAsync(request, cancellationToken);
    }

    private static void ApplyHeaders(HttpRequestMessage request, IDictionary<string, string>? headers)
    {
        if (headers is null)
        {
            return;
        }

        foreach (var header in headers)
        {
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);

        try
        {
            _logger.LogInformation("Calling ICEGATE {Method} {Url}", request.Method, request.RequestUri?.GetLeftPart(UriPartial.Path));
            return await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new IcegateTransientException(IcegateErrorCode.Timeout, "The request to ICEGATE timed out.", null, ex);
        }
        catch (HttpRequestException ex)
        {
            throw new IcegateTransientException(IcegateErrorCode.NetworkFailure, "A network failure occurred while communicating with ICEGATE.", null, ex);
        }
    }
}

public static class HttpResponseMessageExtensions
{
    /// <summary>Throws a typed <see cref="IcegateIntegrationException"/> if the response was not successful; no-op otherwise.</summary>
    public static async Task EnsureIcegateSuccessAsync(this HttpResponseMessage response, CancellationToken cancellationToken = default)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        throw await IcegateErrorMapper.MapAsync(response, cancellationToken);
    }
}

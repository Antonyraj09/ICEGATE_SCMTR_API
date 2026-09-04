using System.Text.Json;
using Icegate.Integration.Models.Common;

namespace Icegate.Integration.Helpers;

/// <summary>
/// Maps a non-success HTTP response from ICEGATE into the appropriate typed exception
/// (business validation vs. token vs. transient), while preserving the original ICEGATE
/// error message verbatim wherever it can be extracted from the response body.
/// </summary>
public static class IcegateErrorMapper
{
    public static async Task<IcegateIntegrationException> MapAsync(HttpResponseMessage response, CancellationToken cancellationToken = default)
    {
        var body = await SafeReadBodyAsync(response, cancellationToken);
        var message = ExtractMessage(body) ?? IcegateDocumentedErrors.UnexpectedError;
        var status = (int)response.StatusCode;

        return status switch
        {
            401 => new IcegateTokenException(
                IcegateErrorCode.TokenInvalidOrExpired,
                LooksLikeMissingToken(message) ? IcegateDocumentedErrors.TokenMissing : IcegateDocumentedErrors.TokenInvalidOrExpired),

            403 when LooksLikeTokenIssue(message) =>
                new IcegateTokenException(IcegateErrorCode.TokenInvalidOrExpired, message),

            403 => new IcegateBusinessValidationException(IcegateErrorCode.UnauthorizedSender, message, status),

            400 or 404 or 409 or 422 =>
                new IcegateBusinessValidationException(IcegateErrorCode.BusinessValidationFailed, message, status),

            408 => new IcegateTransientException(IcegateErrorCode.Timeout, message, status),
            429 => new IcegateTransientException(IcegateErrorCode.IcegateUnavailable, message, status),
            500 or 502 or 503 => new IcegateTransientException(IcegateErrorCode.IcegateUnavailable, message, status),
            504 => new IcegateTransientException(IcegateErrorCode.Timeout, message, status),

            _ => new IcegateTransientException(IcegateErrorCode.IcegateUnavailable, message, status)
        };
    }

    private static bool LooksLikeTokenIssue(string message) =>
        message.Contains("token", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeMissingToken(string message) =>
        message.Contains("missing", StringComparison.OrdinalIgnoreCase);

    private static async Task<string?> SafeReadBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    private static string? ExtractMessage(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            foreach (var propertyName in new[] { "errorMessage", "message", "error", "description" })
            {
                if (root.ValueKind == JsonValueKind.Object &&
                    root.TryGetProperty(propertyName, out var element) &&
                    element.ValueKind == JsonValueKind.String)
                {
                    var value = element.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
                }
            }
        }
        catch (JsonException)
        {
            // Body was not JSON - fall through and return the raw (truncated) body below.
        }

        return body.Length > 1000 ? body[..1000] : body;
    }
}

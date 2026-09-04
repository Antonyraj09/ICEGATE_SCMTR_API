using System.Text.Json;
using Icegate.Integration.Models.Common;

namespace Icegate.Integration.Middleware;

/// <summary>
/// Central exception handler. Maps typed ICEGATE integration exceptions to the standard
/// <see cref="IcegateApiResult{T}"/> envelope with an appropriate HTTP status code, preserving
/// the original ICEGATE business validation message verbatim. Never leaks stack traces,
/// tokens, API keys, or credentials to the caller.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleAsync(context, ex);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception exception)
    {
        var correlationId = context.GetCorrelationId();
        var (statusCode, message, errorMessage) = Map(exception);

        _logger.LogError(
            exception,
            "Unhandled exception while processing {Method} {Path}. CorrelationId={CorrelationId} StatusCode={StatusCode}",
            context.Request.Method, context.Request.Path, correlationId, statusCode);

        var result = IcegateApiResult<object>.Fail(message, errorMessage, correlationId, statusCode);

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = statusCode;

        await context.Response.WriteAsync(JsonSerializer.Serialize(result, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }));
    }

    private static (int StatusCode, string Message, string? ErrorMessage) Map(Exception exception) => exception switch
    {
        IcegateRequestValidationException ex => (400, "The request failed validation.", ex.Message),

        IcegateBusinessValidationException ex => (ex.HttpStatusCode == 0 ? 422 : ex.HttpStatusCode, "ICEGATE business validation failed.", ex.Message),

        IcegateAckNotYetAvailableException ex => (202, "Acknowledgement is not yet available. Please retry later.", ex.Message),

        IcegateTokenException ex => (401, "ICEGATE authentication failed.", ex.Message),

        IcegateEndpointNotConfirmedException ex => (503, "This ICEGATE endpoint has not been confirmed yet.", ex.Message),

        IcegateMultipartParseException ex => (502, "Failed to parse the ICEGATE response.", ex.Message),

        IcegateTransientException ex => (ex.HttpStatusCode is >= 400 and < 600 ? ex.HttpStatusCode.Value : 503, "ICEGATE is temporarily unavailable. Please retry.", ex.Message),

        IcegateIntegrationException ex => (500, IcegateDocumentedErrors.UnexpectedError, ex.Message),

        _ => (500, IcegateDocumentedErrors.UnexpectedError, null)
    };
}

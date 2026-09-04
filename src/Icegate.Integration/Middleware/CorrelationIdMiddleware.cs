using Serilog.Context;

namespace Icegate.Integration.Middleware;

/// <summary>
/// Assigns a correlation ID to every inbound request (reusing one supplied via the
/// X-Correlation-Id request header, or generating a new "CORR-yyyyMMdd-XXXXXX" style ID),
/// exposes it on HttpContext.Items and the response header, and pushes it into the
/// structured logging context for the lifetime of the request.
/// </summary>
public class CorrelationIdMiddleware
{
    public const string HttpContextItemKey = "CorrelationId";
    public const string HeaderName = "X-Correlation-Id";

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context);

        context.Items[HttpContextItemKey] = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(HeaderName, out var supplied) &&
            !string.IsNullOrWhiteSpace(supplied))
        {
            return supplied.ToString();
        }

        return GenerateCorrelationId();
    }

    private static string GenerateCorrelationId()
    {
        var datePart = DateTime.UtcNow.ToString("yyyyMMdd");
        var randomPart = Random.Shared.Next(0, 1_000_000).ToString("D6");
        return $"CORR-{datePart}-{randomPart}";
    }
}

public static class HttpContextCorrelationExtensions
{
    public static string GetCorrelationId(this HttpContext context) =>
        context.Items.TryGetValue(CorrelationIdMiddleware.HttpContextItemKey, out var value) && value is string id
            ? id
            : "CORR-UNKNOWN";
}

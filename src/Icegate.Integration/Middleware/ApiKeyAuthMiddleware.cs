using System.Text.Json;
using Icegate.Integration.Configuration;
using Icegate.Integration.Models.Common;
using Microsoft.Extensions.Options;

namespace Icegate.Integration.Middleware;

/// <summary>
/// Secures OUR .NET 8 API (consumed by the legacy .NET Framework 4.0 application) with a
/// simple internal API key mechanism (X-API-KEY by default), deliberately separate from
/// ICEGATE's own authentication. Health/Swagger paths are exempt.
/// </summary>
public class ApiKeyAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IOptionsMonitor<InternalApiSettings> _settings;
    private readonly ILogger<ApiKeyAuthMiddleware> _logger;

    public ApiKeyAuthMiddleware(RequestDelegate next, IOptionsMonitor<InternalApiSettings> settings, ILogger<ApiKeyAuthMiddleware> logger)
    {
        _next = next;
        _settings = settings;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var settings = _settings.CurrentValue;
        var path = context.Request.Path.Value ?? string.Empty;

        if (settings.AnonymousPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            await _next(context);
            return;
        }

        if (settings.ApiKeys.Length == 0)
        {
            // Internal API key not configured - fail closed rather than silently allowing all traffic.
            _logger.LogError("InternalApi:ApiKeys is not configured. Rejecting request to {Path}.", path);
            await WriteUnauthorizedAsync(context, "Internal API authentication is not configured.");
            return;
        }

        if (!context.Request.Headers.TryGetValue(settings.ApiKeyHeaderName, out var suppliedKey) ||
            string.IsNullOrWhiteSpace(suppliedKey))
        {
            await WriteUnauthorizedAsync(context, $"Missing required '{settings.ApiKeyHeaderName}' header.");
            return;
        }

        var isValid = settings.ApiKeys.Any(k => CryptographicallyEqual(k, suppliedKey.ToString()));
        if (!isValid)
        {
            _logger.LogWarning("Rejected request to {Path}: invalid internal API key.", path);
            await WriteUnauthorizedAsync(context, "Invalid API key.");
            return;
        }

        await _next(context);
    }

    private static bool CryptographicallyEqual(string a, string b)
    {
        var aBytes = System.Text.Encoding.UTF8.GetBytes(a);
        var bBytes = System.Text.Encoding.UTF8.GetBytes(b);
        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
    }

    private static async Task WriteUnauthorizedAsync(HttpContext context, string errorMessage)
    {
        var correlationId = context.GetCorrelationId();
        var result = IcegateApiResult<object>.Fail("Unauthorized.", errorMessage, correlationId, StatusCodes.Status401Unauthorized);

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsync(JsonSerializer.Serialize(result, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }));
    }
}

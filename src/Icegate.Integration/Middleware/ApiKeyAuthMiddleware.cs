using System.Text.Json;
using Icegate.Integration.Configuration;
using Icegate.Integration.Models.Common;
using Icegate.Integration.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace Icegate.Integration.Middleware;

/// <summary>
/// Secures OUR .NET 8 API (consumed by one or more onboarded clients' legacy .NET Framework
/// 4.0 applications) with a simple internal API key mechanism (X-API-KEY by default),
/// deliberately separate from ICEGATE's own authentication. The supplied key is resolved
/// against <see cref="IIcegateClientRegistry"/> - each client has its own key and its own
/// ICEGATE identity, so a valid key both authenticates the caller AND selects which
/// ICEGATE credentials/defaults the rest of the pipeline uses. Health/Swagger paths are exempt.
/// </summary>
public class ApiKeyAuthMiddleware
{
    public const string HttpContextItemKey = "ClientId";

    private readonly RequestDelegate _next;
    private readonly IOptionsMonitor<InternalApiSettings> _settings;
    private readonly IIcegateClientRegistry _clientRegistry;
    private readonly ILogger<ApiKeyAuthMiddleware> _logger;

    public ApiKeyAuthMiddleware(
        RequestDelegate next,
        IOptionsMonitor<InternalApiSettings> settings,
        IIcegateClientRegistry clientRegistry,
        ILogger<ApiKeyAuthMiddleware> logger)
    {
        _next = next;
        _settings = settings;
        _clientRegistry = clientRegistry;
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

        if (!context.Request.Headers.TryGetValue(settings.ApiKeyHeaderName, out var suppliedKey) ||
            string.IsNullOrWhiteSpace(suppliedKey))
        {
            await WriteUnauthorizedAsync(context, $"Missing required '{settings.ApiKeyHeaderName}' header.");
            return;
        }

        if (!_clientRegistry.TryResolveByApiKey(suppliedKey.ToString(), out var client) || client is null)
        {
            _logger.LogWarning("Rejected request to {Path}: no enabled client matches the supplied API key.", path);
            await WriteUnauthorizedAsync(context, "Invalid API key.");
            return;
        }

        context.Items[HttpContextItemKey] = client.ClientId;

        await _next(context);
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

public static class HttpContextClientExtensions
{
    /// <summary>The ClientId resolved by <see cref="ApiKeyAuthMiddleware"/> for the current request.</summary>
    public static string GetClientId(this HttpContext context) =>
        context.Items.TryGetValue(ApiKeyAuthMiddleware.HttpContextItemKey, out var value) && value is string clientId
            ? clientId
            : throw new InvalidOperationException(
                "No ClientId on HttpContext - ApiKeyAuthMiddleware must run before any code that calls GetClientId().");
}

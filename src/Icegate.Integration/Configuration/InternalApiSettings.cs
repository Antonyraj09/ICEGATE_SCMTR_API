namespace Icegate.Integration.Configuration;

/// <summary>
/// Settings for securing OUR .NET 8 API (consumed by the legacy .NET Framework 4.0
/// application). Deliberately separate from <see cref="IcegateSettings"/> — the internal
/// API key must never be the same value as the ICEGATE API key.
/// </summary>
public class InternalApiSettings
{
    public const string SectionName = "InternalApi";

    /// <summary>Header name expected on every inbound request, e.g. "X-API-KEY".</summary>
    public string ApiKeyHeaderName { get; set; } = "X-API-KEY";

    /// <summary>
    /// One or more valid internal API keys (comma separated in config, or supplied via
    /// environment variable / secret manager). Never checked into source control with real values.
    /// </summary>
    public string[] ApiKeys { get; set; } = Array.Empty<string>();

    /// <summary>Paths excluded from internal API key enforcement (health checks, swagger).</summary>
    public string[] AnonymousPaths { get; set; } = { "/health", "/api/icegate/health", "/swagger" };
}

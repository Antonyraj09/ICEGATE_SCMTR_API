namespace Icegate.Integration.Configuration;

/// <summary>
/// Settings for securing OUR .NET 8 API (consumed by the legacy .NET Framework 4.0
/// application(s)). Deliberately separate from <see cref="IcegateSettings"/> - an internal
/// API key must never be the same value as any client's ICEGATE API key. The keys
/// themselves are NOT configured here anymore - each onboarded client has its own key,
/// registered via the "IcegateClients" section (see <see cref="IcegateClientSettings"/> and
/// docs/Multi_Client_Onboarding.md) and resolved at request time by
/// <see cref="Services.Interfaces.IIcegateClientRegistry"/>.
/// </summary>
public class InternalApiSettings
{
    public const string SectionName = "InternalApi";

    /// <summary>Header name expected on every inbound request, e.g. "X-API-KEY".</summary>
    public string ApiKeyHeaderName { get; set; } = "X-API-KEY";

    /// <summary>Paths excluded from internal API key enforcement (health checks, swagger).</summary>
    public string[] AnonymousPaths { get; set; } = { "/health", "/api/icegate/health", "/swagger" };
}

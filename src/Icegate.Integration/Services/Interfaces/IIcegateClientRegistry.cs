using Icegate.Integration.Configuration;

namespace Icegate.Integration.Services.Interfaces;

/// <summary>
/// Resolves onboarded clients (each with their own ICEGATE identity) by the internal API key
/// they authenticate with, or by their ClientId. Backed by the "IcegateClients" configuration
/// section (see docs/Multi_Client_Onboarding.md for how to add a new client).
/// </summary>
public interface IIcegateClientRegistry
{
    bool TryResolveByApiKey(string apiKey, out IcegateClientSettings? client);

    bool TryGetByClientId(string clientId, out IcegateClientSettings? client);
}

using System.Security.Cryptography;
using Icegate.Integration.Configuration;
using Icegate.Integration.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace Icegate.Integration.Services;

/// <inheritdoc cref="IIcegateClientRegistry"/>
public class IcegateClientRegistry : IIcegateClientRegistry
{
    private readonly IOptionsMonitor<List<IcegateClientSettings>> _clients;

    public IcegateClientRegistry(IOptionsMonitor<List<IcegateClientSettings>> clients)
    {
        _clients = clients;
    }

    public bool TryResolveByApiKey(string apiKey, out IcegateClientSettings? client)
    {
        client = null;

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return false;
        }

        foreach (var candidate in _clients.CurrentValue)
        {
            if (candidate.Enabled && CryptographicallyEqual(candidate.ApiKey, apiKey))
            {
                client = candidate;
                return true;
            }
        }

        return false;
    }

    public bool TryGetByClientId(string clientId, out IcegateClientSettings? client)
    {
        client = _clients.CurrentValue
            .FirstOrDefault(c => string.Equals(c.ClientId, clientId, StringComparison.OrdinalIgnoreCase));

        return client is not null;
    }

    private static bool CryptographicallyEqual(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
        {
            return false;
        }

        var aBytes = System.Text.Encoding.UTF8.GetBytes(a);
        var bBytes = System.Text.Encoding.UTF8.GetBytes(b);

        // Lengths differing is not itself timing-sensitive information worth hiding here,
        // but FixedTimeEquals requires equal-length spans.
        if (aBytes.Length != bBytes.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
    }
}

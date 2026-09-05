using Icegate.Integration.Configuration;
using Icegate.Integration.Services;
using Xunit;

namespace Icegate.Integration.Tests.Services;

public class IcegateClientRegistryTests
{
    private static List<IcegateClientSettings> SampleClients() => new()
    {
        new IcegateClientSettings
        {
            ClientId = "ACME-CFS",
            ApiKey = "acme-internal-key",
            EncryptedCredentialData = "acme-encrypted",
            DefaultSenderId = "ACMESENDER",
            DefaultIcegateId = "ACMEICG",
            DefaultCustodianCode = "ACMECUST",
            Enabled = true
        },
        new IcegateClientSettings
        {
            ClientId = "BETA-LOGISTICS",
            ApiKey = "beta-internal-key",
            EncryptedCredentialData = "beta-encrypted",
            DefaultSenderId = "BETASENDER",
            DefaultIcegateId = "BETAICG",
            DefaultCustodianCode = "BETACUST",
            Enabled = false
        }
    };

    private static IcegateClientRegistry Sut() => new(TestOptionsMonitor.Create(SampleClients()));

    [Fact]
    public void TryResolveByApiKey_FindsEnabledClientByExactKey()
    {
        var found = Sut().TryResolveByApiKey("acme-internal-key", out var client);

        Assert.True(found);
        Assert.Equal("ACME-CFS", client!.ClientId);
    }

    [Fact]
    public void TryResolveByApiKey_RejectsDisabledClientEvenWithCorrectKey()
    {
        var found = Sut().TryResolveByApiKey("beta-internal-key", out var client);

        Assert.False(found);
        Assert.Null(client);
    }

    [Fact]
    public void TryResolveByApiKey_RejectsUnknownKey()
    {
        var found = Sut().TryResolveByApiKey("not-a-real-key", out var client);

        Assert.False(found);
        Assert.Null(client);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryResolveByApiKey_RejectsBlankInput(string? apiKey)
    {
        var found = Sut().TryResolveByApiKey(apiKey!, out var client);

        Assert.False(found);
        Assert.Null(client);
    }

    [Fact]
    public void TryResolveByApiKey_NeverMatchesAnotherClientsKey()
    {
        // ACME's key must never resolve to BETA's identity or vice versa - this is the
        // core isolation guarantee for multi-client access.
        var sut = Sut();

        sut.TryResolveByApiKey("acme-internal-key", out var acme);
        sut.TryResolveByApiKey("beta-internal-key", out var beta);

        Assert.NotEqual(acme?.ClientId, beta?.ClientId);
    }

    [Fact]
    public void TryGetByClientId_IsCaseInsensitive()
    {
        var found = Sut().TryGetByClientId("acme-cfs", out var client);

        Assert.True(found);
        Assert.Equal("ACME-CFS", client!.ClientId);
    }

    [Fact]
    public void TryGetByClientId_ReturnsFalseForUnknownClient()
    {
        var found = Sut().TryGetByClientId("NOT-REGISTERED", out var client);

        Assert.False(found);
        Assert.Null(client);
    }
}

using Icegate.Integration.Configuration;
using Icegate.Integration.Services;
using Icegate.Integration.Services.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Icegate.Integration.Tests.Services;

public class IcegateTokenServiceTests
{
    private static IOptionsMonitor<IcegateSettings> Settings(int safetyBufferSeconds = 60) =>
        TestOptionsMonitor.Create(new IcegateSettings { TokenSafetyBufferSeconds = safetyBufferSeconds, TokenNominalLifetimeSeconds = 900 });

    [Fact]
    public async Task GetTokenAsync_GeneratesTokenOnFirstCall()
    {
        var authMock = new Mock<IIcegateAuthenticationService>();
        authMock.Setup(a => a.AuthenticateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(("token-1", DateTimeOffset.UtcNow.AddMinutes(15)));

        var sut = new IcegateTokenService(authMock.Object, Settings(), NullLogger<IcegateTokenService>.Instance);

        var token = await sut.GetTokenAsync();

        Assert.Equal("token-1", token);
        authMock.Verify(a => a.AuthenticateAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetTokenAsync_ReusesCachedTokenWithinValidity()
    {
        var authMock = new Mock<IIcegateAuthenticationService>();
        authMock.Setup(a => a.AuthenticateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(("token-1", DateTimeOffset.UtcNow.AddMinutes(15)));

        var sut = new IcegateTokenService(authMock.Object, Settings(), NullLogger<IcegateTokenService>.Instance);

        var first = await sut.GetTokenAsync();
        var second = await sut.GetTokenAsync();
        var third = await sut.GetTokenAsync();

        Assert.Equal(first, second);
        Assert.Equal(first, third);
        authMock.Verify(a => a.AuthenticateAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetTokenAsync_RegeneratesWhenWithinSafetyBufferOfExpiry()
    {
        var authMock = new Mock<IIcegateAuthenticationService>();
        var callCount = 0;
        authMock.Setup(a => a.AuthenticateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                // First token expires almost immediately (well within the safety buffer),
                // so a second call must trigger regeneration.
                var expiry = callCount == 1 ? DateTimeOffset.UtcNow.AddSeconds(5) : DateTimeOffset.UtcNow.AddMinutes(15);
                return ($"token-{callCount}", expiry);
            });

        // 60 second safety buffer > 5 second expiry above -> token is immediately considered unusable.
        var sut = new IcegateTokenService(authMock.Object, Settings(safetyBufferSeconds: 60), NullLogger<IcegateTokenService>.Instance);

        var first = await sut.GetTokenAsync();
        var second = await sut.GetTokenAsync();

        Assert.Equal("token-1", first);
        Assert.Equal("token-2", second);
        authMock.Verify(a => a.AuthenticateAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task InvalidateCachedToken_ForcesRegenerationOnNextCall()
    {
        var authMock = new Mock<IIcegateAuthenticationService>();
        var callCount = 0;
        authMock.Setup(a => a.AuthenticateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return ($"token-{callCount}", DateTimeOffset.UtcNow.AddMinutes(15));
            });

        var sut = new IcegateTokenService(authMock.Object, Settings(), NullLogger<IcegateTokenService>.Instance);

        var first = await sut.GetTokenAsync();
        sut.InvalidateCachedToken();
        var second = await sut.GetTokenAsync();

        Assert.Equal("token-1", first);
        Assert.Equal("token-2", second);
        authMock.Verify(a => a.AuthenticateAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task GetTokenAsync_ConcurrentCalls_OnlyGenerateTokenOnce()
    {
        var authMock = new Mock<IIcegateAuthenticationService>();
        var callCount = 0;
        authMock.Setup(a => a.AuthenticateAsync(It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                Interlocked.Increment(ref callCount);
                await Task.Delay(50);
                return ("token-1", DateTimeOffset.UtcNow.AddMinutes(15));
            });

        var sut = new IcegateTokenService(authMock.Object, Settings(), NullLogger<IcegateTokenService>.Instance);

        var tasks = Enumerable.Range(0, 10).Select(_ => sut.GetTokenAsync());
        var results = await Task.WhenAll(tasks);

        Assert.All(results, r => Assert.Equal("token-1", r));
        Assert.Equal(1, callCount);
    }
}

using System.Net;
using Icegate.Integration.Models.Common;
using Icegate.Integration.Services;
using Icegate.Integration.Services.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Icegate.Integration.Tests.Services;

public class TokenRetryExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_SuccessOnFirstAttempt_NeverInvalidatesToken()
    {
        var tokenServiceMock = new Mock<IIcegateTokenService>();
        tokenServiceMock.Setup(t => t.GetTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync("token-1");

        var sut = new TokenRetryExecutor(tokenServiceMock.Object, NullLogger<TokenRetryExecutor>.Instance);

        var callCount = 0;
        var response = await sut.ExecuteAsync((token, ct) =>
        {
            callCount++;
            Assert.Equal("token-1", token);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        Assert.Equal(1, callCount);
        Assert.True(response.IsSuccessStatusCode);
        tokenServiceMock.Verify(t => t.InvalidateCachedToken(), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_TokenRejectedOnce_ClearsTokenAndRetriesExactlyOnce()
    {
        var tokenServiceMock = new Mock<IIcegateTokenService>();
        var tokenCallCount = 0;
        tokenServiceMock.Setup(t => t.GetTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => $"token-{++tokenCallCount}");

        var sut = new TokenRetryExecutor(tokenServiceMock.Object, NullLogger<TokenRetryExecutor>.Instance);

        var attempt = 0;
        var response = await sut.ExecuteAsync((token, ct) =>
        {
            attempt++;
            if (attempt == 1)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
            }

            Assert.Equal("token-2", token);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        Assert.Equal(2, attempt);
        Assert.True(response.IsSuccessStatusCode);
        tokenServiceMock.Verify(t => t.InvalidateCachedToken(), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_TokenRejectedTwice_ThrowsAfterSingleRetry_NeverLoopsForever()
    {
        var tokenServiceMock = new Mock<IIcegateTokenService>();
        tokenServiceMock.Setup(t => t.GetTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync("token-x");

        var sut = new TokenRetryExecutor(tokenServiceMock.Object, NullLogger<TokenRetryExecutor>.Instance);

        var attempt = 0;
        await Assert.ThrowsAsync<IcegateTokenException>(() => sut.ExecuteAsync((token, ct) =>
        {
            attempt++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
        }));

        Assert.Equal(2, attempt); // original + exactly one retry, never more
        tokenServiceMock.Verify(t => t.InvalidateCachedToken(), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_BusinessValidationFailure_NeverInvalidatesTokenOrRetries()
    {
        var tokenServiceMock = new Mock<IIcegateTokenService>();
        tokenServiceMock.Setup(t => t.GetTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync("token-1");

        var sut = new TokenRetryExecutor(tokenServiceMock.Object, NullLogger<TokenRetryExecutor>.Instance);

        var attempt = 0;
        await Assert.ThrowsAsync<IcegateBusinessValidationException>(() => sut.ExecuteAsync((token, ct) =>
        {
            attempt++;
            return Task.FromResult(new HttpResponseMessage((HttpStatusCode)422));
        }));

        Assert.Equal(1, attempt);
        tokenServiceMock.Verify(t => t.InvalidateCachedToken(), Times.Never);
    }
}

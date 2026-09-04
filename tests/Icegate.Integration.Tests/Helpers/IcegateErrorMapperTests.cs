using System.Net;
using Icegate.Integration.Helpers;
using Icegate.Integration.Models.Common;
using Xunit;

namespace Icegate.Integration.Tests.Helpers;

public class IcegateErrorMapperTests
{
    private static HttpResponseMessage BuildResponse(HttpStatusCode statusCode, string? jsonBody = null)
    {
        var response = new HttpResponseMessage(statusCode);
        if (jsonBody is not null)
        {
            response.Content = new StringContent(jsonBody, System.Text.Encoding.UTF8, "application/json");
        }
        return response;
    }

    [Fact]
    public async Task MapAsync_401_MapsToTokenException()
    {
        using var response = BuildResponse(HttpStatusCode.Unauthorized,
            "{ \"errorMessage\": \"" + IcegateDocumentedErrors.TokenInvalidOrExpired + "\" }");

        var exception = await IcegateErrorMapper.MapAsync(response);

        var tokenEx = Assert.IsType<IcegateTokenException>(exception);
        Assert.Equal(IcegateDocumentedErrors.TokenInvalidOrExpired, tokenEx.Message);
    }

    [Fact]
    public async Task MapAsync_422_PreservesOriginalIcegateMessage()
    {
        using var response = BuildResponse((HttpStatusCode)422,
            "{ \"errorMessage\": \"" + IcegateDocumentedErrors.CustodianCodeNotMapped + "\" }");

        var exception = await IcegateErrorMapper.MapAsync(response);

        var businessEx = Assert.IsType<IcegateBusinessValidationException>(exception);
        Assert.Equal(IcegateDocumentedErrors.CustodianCodeNotMapped, businessEx.Message);
        Assert.Equal(422, businessEx.HttpStatusCode);
    }

    [Theory]
    [InlineData(HttpStatusCode.RequestTimeout)]
    [InlineData((HttpStatusCode)429)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    public async Task MapAsync_TransientStatusCodes_MapToTransientException(HttpStatusCode statusCode)
    {
        using var response = BuildResponse(statusCode);

        var exception = await IcegateErrorMapper.MapAsync(response);

        Assert.IsType<IcegateTransientException>(exception);
    }

    [Fact]
    public async Task MapAsync_400_MapsToBusinessValidation_NeverRetried()
    {
        using var response = BuildResponse(HttpStatusCode.BadRequest,
            "{ \"errorMessage\": \"" + IcegateDocumentedErrors.SenderIdRequired + "\" }");

        var exception = await IcegateErrorMapper.MapAsync(response);

        Assert.IsType<IcegateBusinessValidationException>(exception);
        Assert.Equal(IcegateDocumentedErrors.SenderIdRequired, exception.Message);
    }

    [Fact]
    public async Task MapAsync_403WithTokenWording_MapsToTokenException()
    {
        using var response = BuildResponse(HttpStatusCode.Forbidden,
            "{ \"message\": \"" + IcegateDocumentedErrors.TokenMissing + "\" }");

        var exception = await IcegateErrorMapper.MapAsync(response);

        Assert.IsType<IcegateTokenException>(exception);
    }

    [Fact]
    public async Task MapAsync_403WithSenderWording_MapsToBusinessValidation()
    {
        using var response = BuildResponse(HttpStatusCode.Forbidden,
            "{ \"message\": \"" + IcegateDocumentedErrors.AuthorizationFailedSender + "\" }");

        var exception = await IcegateErrorMapper.MapAsync(response);

        Assert.IsType<IcegateBusinessValidationException>(exception);
    }

    [Fact]
    public async Task MapAsync_NonJsonBody_FallsBackToRawBody()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("Internal Server Error - Gateway Down")
        };

        var exception = await IcegateErrorMapper.MapAsync(response);

        Assert.Contains("Gateway Down", exception.Message);
    }
}

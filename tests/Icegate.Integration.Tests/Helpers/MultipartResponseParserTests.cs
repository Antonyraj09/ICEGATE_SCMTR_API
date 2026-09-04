using System.Text;
using Icegate.Integration.Helpers;
using Icegate.Integration.Models.Common;
using Xunit;

namespace Icegate.Integration.Tests.Helpers;

public class MultipartResponseParserTests
{
    [Fact]
    public async Task ParseAsync_ExtractsNamedFieldsAndFilePart()
    {
        using var content = new MultipartFormDataContent("test-boundary-123")
        {
            { new StringContent("SRAVANCFS"), "senderId" },
            { new StringContent("123456789ASOCUCHE01"), "uniqueId" },
            { new StringContent("Acknowledgment has been generated."), "responseDesc" }
        };

        var fileBytes = Encoding.UTF8.GetBytes("{\"ack\":true}");
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        content.Add(fileContent, "file", "CHCAT02_ACK_20260624_162721592.json");

        var parts = await MultipartResponseParser.ParseAsync(content);

        Assert.Equal("SRAVANCFS", MultipartResponseParser.FindField(parts, "senderId"));
        Assert.Equal("123456789ASOCUCHE01", MultipartResponseParser.FindField(parts, "uniqueId"));
        Assert.Equal("Acknowledgment has been generated.", MultipartResponseParser.FindField(parts, "responseDesc"));

        var filePart = MultipartResponseParser.FindFilePart(parts);
        Assert.NotNull(filePart);
        Assert.Equal("CHCAT02_ACK_20260624_162721592.json", filePart!.FileName);
        Assert.Equal(fileBytes, filePart.Content);
    }

    [Fact]
    public async Task ParseAsync_FieldLookupIsCaseInsensitive()
    {
        using var content = new MultipartFormDataContent("boundary-abc")
        {
            { new StringContent("INMAAISLP1"), "IcegateId" }
        };

        var parts = await MultipartResponseParser.ParseAsync(content);

        Assert.Equal("INMAAISLP1", MultipartResponseParser.FindField(parts, "icegateid"));
        Assert.Equal("INMAAISLP1", MultipartResponseParser.FindField(parts, "ICEGATEID"));
    }

    [Fact]
    public async Task ParseAsync_ThrowsWhenContentIsNotMultipart()
    {
        using var content = new StringContent("{\"not\":\"multipart\"}", Encoding.UTF8, "application/json");

        await Assert.ThrowsAsync<IcegateMultipartParseException>(() => MultipartResponseParser.ParseAsync(content));
    }

    [Fact]
    public async Task FindField_ReturnsNullWhenNoCandidateMatches()
    {
        using var content = new MultipartFormDataContent("boundary-xyz")
        {
            { new StringContent("value"), "someOtherField" }
        };

        var parts = await MultipartResponseParser.ParseAsync(content);

        Assert.Null(MultipartResponseParser.FindField(parts, "responseDesc", "responseDescription"));
    }
}

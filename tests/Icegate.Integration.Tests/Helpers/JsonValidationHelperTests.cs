using System.Text;
using Icegate.Integration.Helpers;
using Xunit;

namespace Icegate.Integration.Tests.Helpers;

public class JsonValidationHelperTests
{
    [Fact]
    public async Task IsValidJsonAsync_ReturnsTrue_ForWellFormedObject()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("{ \"a\": 1, \"b\": [1,2,3] }"));

        var (isValid, error) = await JsonValidationHelper.IsValidJsonAsync(stream);

        Assert.True(isValid);
        Assert.Null(error);
    }

    [Fact]
    public async Task IsValidJsonAsync_ReturnsFalse_ForTrailingComma()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("{ \"a\": 1, }"));

        var (isValid, error) = await JsonValidationHelper.IsValidJsonAsync(stream);

        Assert.False(isValid);
        Assert.NotNull(error);
    }

    [Fact]
    public async Task IsValidJsonAsync_ReturnsFalse_ForEmptyContent()
    {
        using var stream = new MemoryStream(Array.Empty<byte>());

        var (isValid, _) = await JsonValidationHelper.IsValidJsonAsync(stream);

        Assert.False(isValid);
    }

    [Fact]
    public async Task IsValidJsonAsync_DoesNotModifyStreamContent()
    {
        var original = "{ \"messageId\": \"CUCHE01\" }";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(original));

        await JsonValidationHelper.IsValidJsonAsync(stream);

        stream.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(stream);
        var afterValidation = await reader.ReadToEndAsync();

        Assert.Equal(original, afterValidation);
    }

    [Fact]
    public void IsValidJson_String_ReturnsFalseForPlainText()
    {
        var isValid = JsonValidationHelper.IsValidJson("not json at all", out var error);

        Assert.False(isValid);
        Assert.NotNull(error);
    }
}

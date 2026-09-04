using System.Text;
using Icegate.Integration.Configuration;
using Icegate.Integration.Helpers;
using Icegate.Integration.Models.Common;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Icegate.Integration.Tests.Helpers;

public class FileValidationHelperTests
{
    private static FileStorageSettings DefaultSettings() => new()
    {
        MaxInboundFileSizeBytes = 1024,
        AllowedInboundExtensions = new[] { ".json" }
    };

    private static IFormFile BuildFormFile(string content, string fileName = "scmtr.json")
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "file", fileName);
    }

    [Fact]
    public async Task ValidateAsync_ReturnsInvalid_WhenFileIsNull()
    {
        var (isValid, errorCode, _) = await FileValidationHelper.ValidateAsync(null, DefaultSettings());

        Assert.False(isValid);
        Assert.Equal(IcegateErrorCode.InvalidFile, errorCode);
    }

    [Fact]
    public async Task ValidateAsync_ReturnsInvalid_WhenFileIsEmpty()
    {
        var file = BuildFormFile(string.Empty);

        var (isValid, errorCode, _) = await FileValidationHelper.ValidateAsync(file, DefaultSettings());

        Assert.False(isValid);
        Assert.Equal(IcegateErrorCode.EmptyFile, errorCode);
    }

    [Fact]
    public async Task ValidateAsync_ReturnsInvalid_WhenFileExceedsMaxSize()
    {
        var file = BuildFormFile(new string('a', 2000));

        var (isValid, errorCode, _) = await FileValidationHelper.ValidateAsync(file, DefaultSettings());

        Assert.False(isValid);
        Assert.Equal(IcegateErrorCode.FileTooLarge, errorCode);
    }

    [Fact]
    public async Task ValidateAsync_ReturnsInvalid_WhenExtensionNotAllowed()
    {
        var file = BuildFormFile("{}", fileName: "scmtr.txt");

        var (isValid, errorCode, _) = await FileValidationHelper.ValidateAsync(file, DefaultSettings());

        Assert.False(isValid);
        Assert.Equal(IcegateErrorCode.InvalidExtension, errorCode);
    }

    [Fact]
    public async Task ValidateAsync_ReturnsInvalid_WhenJsonIsMalformed()
    {
        var file = BuildFormFile("{ this is not json");

        var (isValid, errorCode, _) = await FileValidationHelper.ValidateAsync(file, DefaultSettings());

        Assert.False(isValid);
        Assert.Equal(IcegateErrorCode.InvalidJson, errorCode);
    }

    [Fact]
    public async Task ValidateAsync_ReturnsValid_ForWellFormedScmtrJson()
    {
        var file = BuildFormFile("{ \"messageId\": \"CUCHE01\", \"items\": [1, 2, 3] }");

        var (isValid, _, error) = await FileValidationHelper.ValidateAsync(file, DefaultSettings());

        Assert.True(isValid);
        Assert.Null(error);
    }

    [Fact]
    public async Task ValidateAsync_ReturnsValid_ForJsonArrayRoot()
    {
        var file = BuildFormFile("[ { \"a\": 1 }, { \"b\": 2 } ]");

        var (isValid, _, _) = await FileValidationHelper.ValidateAsync(file, DefaultSettings());

        Assert.True(isValid);
    }
}

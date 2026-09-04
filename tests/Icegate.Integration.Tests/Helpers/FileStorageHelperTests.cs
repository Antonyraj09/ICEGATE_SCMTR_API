using System.Text;
using Icegate.Integration.Helpers;
using Xunit;

namespace Icegate.Integration.Tests.Helpers;

public class FileStorageHelperTests : IDisposable
{
    private readonly string _tempRoot;

    public FileStorageHelperTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "icegate-tests-" + Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task SaveAsync_SanitizesPathTraversalInFileName()
    {
        var content = Encoding.UTF8.GetBytes("{}");

        var result = await FileStorageHelper.SaveAsync(_tempRoot, "../../etc/passwd.json", content);

        Assert.StartsWith(Path.GetFullPath(_tempRoot), result.FilePath);
        Assert.True(File.Exists(result.FilePath));
        Assert.DoesNotContain("..", result.GeneratedFileName);
    }

    [Fact]
    public async Task SaveAsync_GeneratesUniqueFileNamesForRepeatedUploads()
    {
        var content = Encoding.UTF8.GetBytes("{}");

        var first = await FileStorageHelper.SaveAsync(_tempRoot, "scmtr.json", content);
        var second = await FileStorageHelper.SaveAsync(_tempRoot, "scmtr.json", content);

        Assert.NotEqual(first.GeneratedFileName, second.GeneratedFileName);
        Assert.True(File.Exists(first.FilePath));
        Assert.True(File.Exists(second.FilePath));
    }

    [Fact]
    public async Task SaveAsync_ComputesConsistentSha256Hash()
    {
        var content = Encoding.UTF8.GetBytes("{ \"a\": 1 }");

        var result = await FileStorageHelper.SaveAsync(_tempRoot, "scmtr.json", content);
        var expectedHash = await FileStorageHelper.ComputeHashAsync(new MemoryStream(content));

        Assert.Equal(expectedHash, result.FileHash);
        Assert.Equal(64, result.FileHash.Length); // SHA-256 hex string
    }

    [Fact]
    public async Task SaveAsync_PreservesOriginalFileNameInResultMetadata()
    {
        var content = Encoding.UTF8.GetBytes("{}");

        var result = await FileStorageHelper.SaveAsync(_tempRoot, "CHCAT02_20260904.json", content);

        Assert.Equal("CHCAT02_20260904.json", result.OriginalFileName);
        Assert.Equal(content.Length, result.FileSize);
    }
}

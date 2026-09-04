using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Icegate.Integration.Helpers;

/// <summary>Descriptor of a file persisted to disk by <see cref="FileStorageHelper"/>.</summary>
public class StoredFileInfoResult
{
    public string OriginalFileName { get; init; } = string.Empty;

    public string GeneratedFileName { get; init; } = string.Empty;

    public string FilePath { get; init; } = string.Empty;

    public long FileSize { get; init; }

    public string FileHash { get; init; } = string.Empty;

    public DateTime CreatedDate { get; init; }
}

/// <summary>
/// Persists inbound SCMTR files, ACKs, and ZIP ACKs to configured, safe storage locations.
/// Guards against path traversal, arbitrary overwrite, and unsafe extensions.
/// </summary>
public static class FileStorageHelper
{
    private static readonly Regex UnsafeFileNameChars = new("[^a-zA-Z0-9._-]", RegexOptions.Compiled);

    public static async Task<StoredFileInfoResult> SaveAsync(
        string rootPath,
        string originalFileName,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        var safeRoot = ResolveSafeRoot(rootPath);
        Directory.CreateDirectory(safeRoot);

        var generatedFileName = BuildSafeFileName(originalFileName);
        var destinationPath = ResolveSafeDestination(safeRoot, generatedFileName);

        using var sha256 = SHA256.Create();
        await using (var fileStream = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            if (content.CanSeek)
            {
                content.Seek(0, SeekOrigin.Begin);
            }

            await using var hashingStream = new CryptoStream(fileStream, sha256, CryptoStreamMode.Write, leaveOpen: true);
            await content.CopyToAsync(hashingStream, cancellationToken);
        }

        var hash = Convert.ToHexString(sha256.Hash ?? Array.Empty<byte>()).ToLowerInvariant();
        var fileSize = new FileInfo(destinationPath).Length;

        return new StoredFileInfoResult
        {
            OriginalFileName = originalFileName,
            GeneratedFileName = generatedFileName,
            FilePath = destinationPath,
            FileSize = fileSize,
            FileHash = hash,
            CreatedDate = DateTime.UtcNow
        };
    }

    public static async Task<StoredFileInfoResult> SaveAsync(
        string rootPath,
        string originalFileName,
        byte[] content,
        CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryStream(content, writable: false);
        return await SaveAsync(rootPath, originalFileName, stream, cancellationToken);
    }

    /// <summary>Computes a SHA-256 hash for a stream without persisting it (used for pre-submission integrity checks).</summary>
    public static async Task<string> ComputeHashAsync(Stream content, CancellationToken cancellationToken = default)
    {
        if (content.CanSeek)
        {
            content.Seek(0, SeekOrigin.Begin);
        }

        using var sha256 = SHA256.Create();
        var hash = await sha256.ComputeHashAsync(content, cancellationToken);

        if (content.CanSeek)
        {
            content.Seek(0, SeekOrigin.Begin);
        }

        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string ResolveSafeRoot(string rootPath)
    {
        var basePath = AppContext.BaseDirectory;
        var combined = Path.IsPathRooted(rootPath) ? rootPath : Path.Combine(basePath, rootPath);
        return Path.GetFullPath(combined);
    }

    private static string BuildSafeFileName(string originalFileName)
    {
        var extension = Path.GetExtension(originalFileName);
        if (string.IsNullOrWhiteSpace(extension) || extension.Length > 10)
        {
            extension = ".dat";
        }

        var stem = Path.GetFileNameWithoutExtension(originalFileName);
        stem = UnsafeFileNameChars.Replace(stem, "_");
        if (stem.Length > 80)
        {
            stem = stem[..80];
        }
        if (string.IsNullOrWhiteSpace(stem))
        {
            stem = "file";
        }

        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmssfff");
        var unique = Guid.NewGuid().ToString("N")[..8];

        return $"{stem}_{timestamp}_{unique}{extension}";
    }

    private static string ResolveSafeDestination(string safeRoot, string generatedFileName)
    {
        var destinationPath = Path.GetFullPath(Path.Combine(safeRoot, generatedFileName));

        if (!destinationPath.StartsWith(safeRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Resolved file path escapes the configured storage root.");
        }

        if (File.Exists(destinationPath))
        {
            throw new IOException($"A file already exists at the generated path '{destinationPath}'.");
        }

        return destinationPath;
    }
}

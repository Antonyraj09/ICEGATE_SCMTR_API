using Icegate.Integration.Configuration;
using Icegate.Integration.Models.Common;
using Microsoft.AspNetCore.Http;

namespace Icegate.Integration.Helpers;

/// <summary>
/// Pre-submission validation for inbound SCMTR files: existence, non-empty, allowed extension,
/// maximum size, and JSON syntax. Never inspects or alters SCMTR business content.
/// </summary>
public static class FileValidationHelper
{
    public static async Task<(bool IsValid, string ErrorCode, string? Error)> ValidateAsync(
        IFormFile? file,
        FileStorageSettings settings,
        CancellationToken cancellationToken = default)
    {
        if (file is null)
        {
            return (false, IcegateErrorCode.InvalidFile, "No file was supplied. Attach the SCMTR JSON file as 'file' in the multipart/form-data request.");
        }

        if (file.Length == 0)
        {
            return (false, IcegateErrorCode.EmptyFile, "The uploaded file is empty.");
        }

        if (file.Length > settings.MaxInboundFileSizeBytes)
        {
            return (false, IcegateErrorCode.FileTooLarge,
                $"The uploaded file ({file.Length} bytes) exceeds the maximum allowed size of {settings.MaxInboundFileSizeBytes} bytes.");
        }

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension) ||
            !settings.AllowedInboundExtensions.Any(ext => string.Equals(ext, extension, StringComparison.OrdinalIgnoreCase)))
        {
            return (false, IcegateErrorCode.InvalidExtension,
                $"File extension '{extension}' is not allowed. Allowed extensions: {string.Join(", ", settings.AllowedInboundExtensions)}.");
        }

        await using var stream = file.OpenReadStream();
        var (isValidJson, jsonError) = await JsonValidationHelper.IsValidJsonAsync(stream, cancellationToken);
        if (!isValidJson)
        {
            return (false, IcegateErrorCode.InvalidJson, jsonError);
        }

        return (true, string.Empty, null);
    }
}

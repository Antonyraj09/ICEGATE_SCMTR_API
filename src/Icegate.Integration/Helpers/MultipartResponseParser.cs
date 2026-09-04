using Icegate.Integration.Models.Common;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;

namespace Icegate.Integration.Helpers;

/// <summary>One decoded section of a multipart response (a form field or a file part).</summary>
public class MultipartPart
{
    public string? Name { get; set; }

    public string? FileName { get; set; }

    public string ContentType { get; set; } = "application/octet-stream";

    public byte[] Content { get; set; } = Array.Empty<byte>();

    public bool IsFile => !string.IsNullOrWhiteSpace(FileName);

    public string AsString() => System.Text.Encoding.UTF8.GetString(Content);
}

/// <summary>
/// Parses multipart HTTP responses returned by ICEGATE for the Get Acknowledgement and
/// Get Zip Acknowledgement APIs. The document states these responses are multipart and must
/// NOT be assumed to be JSON-only, so this parser makes no assumption about part ordering,
/// count, or whether metadata arrives as individual named fields or a single JSON part.
/// </summary>
public static class MultipartResponseParser
{
    private const int MaxPartCountGuard = 200;

    public static async Task<List<MultipartPart>> ParseAsync(HttpContent content, CancellationToken cancellationToken = default)
    {
        var contentType = content.Headers.ContentType;
        if (contentType is null || contentType.MediaType is null ||
            !contentType.MediaType.Contains("multipart", StringComparison.OrdinalIgnoreCase))
        {
            throw new IcegateMultipartParseException(
                $"Expected a multipart response from ICEGATE but received Content-Type '{contentType?.MediaType ?? "(none)"}'.");
        }

        var boundary = HeaderUtilities.RemoveQuotes(
                contentType.Parameters.FirstOrDefault(p => string.Equals(p.Name, "boundary", StringComparison.OrdinalIgnoreCase))?.Value ?? default)
            .Value;

        if (string.IsNullOrWhiteSpace(boundary))
        {
            throw new IcegateMultipartParseException("The ICEGATE multipart response did not include a boundary parameter.");
        }

        var parts = new List<MultipartPart>();

        try
        {
            await using var stream = await content.ReadAsStreamAsync(cancellationToken);
            var reader = new MultipartReader(boundary, stream);

            MultipartSection? section;
            while ((section = await reader.ReadNextSectionAsync(cancellationToken)) is not null)
            {
                if (parts.Count >= MaxPartCountGuard)
                {
                    throw new IcegateMultipartParseException("The ICEGATE multipart response exceeded the maximum expected number of parts.");
                }

                var disposition = section.GetContentDispositionHeader();

                using var buffer = new MemoryStream();
                await section.Body.CopyToAsync(buffer, cancellationToken);

                parts.Add(new MultipartPart
                {
                    Name = disposition?.Name.HasValue == true ? disposition.Name.Value : null,
                    FileName = ResolveFileName(disposition),
                    ContentType = section.ContentType ?? "application/octet-stream",
                    Content = buffer.ToArray()
                });
            }
        }
        catch (IcegateMultipartParseException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new IcegateMultipartParseException("Failed to parse the ICEGATE multipart response.", ex);
        }

        if (parts.Count == 0)
        {
            throw new IcegateMultipartParseException("The ICEGATE multipart response did not contain any parts.");
        }

        return parts;
    }

    private static string? ResolveFileName(ContentDispositionHeaderValue? disposition)
    {
        if (disposition is null)
        {
            return null;
        }

        if (disposition.FileNameStar.HasValue)
        {
            return disposition.FileNameStar.Value;
        }

        return disposition.FileName.HasValue ? disposition.FileName.Value : null;
    }

    /// <summary>
    /// Case-insensitively looks up a named, non-file text part and returns its string value.
    /// </summary>
    public static string? FindField(IEnumerable<MultipartPart> parts, params string[] candidateNames)
    {
        foreach (var name in candidateNames)
        {
            var match = parts.FirstOrDefault(p =>
                !p.IsFile && string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                return match.AsString().Trim().Trim('"');
            }
        }

        return null;
    }

    /// <summary>Returns the first part that carries a file name (the ACK/ZIP payload part).</summary>
    public static MultipartPart? FindFilePart(IEnumerable<MultipartPart> parts) =>
        parts.FirstOrDefault(p => p.IsFile);
}

using System.Text.Json;

namespace Icegate.Integration.Helpers;

/// <summary>
/// Validates that an uploaded SCMTR file is syntactically valid JSON, without parsing it
/// into a business object, renaming properties, or altering its content in any way.
/// The integration layer is responsible for transport, not SCMTR business content.
/// </summary>
public static class JsonValidationHelper
{
    public static async Task<(bool IsValid, string? Error)> IsValidJsonAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        if (stream.CanSeek)
        {
            stream.Seek(0, SeekOrigin.Begin);
        }

        try
        {
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            if (document.RootElement.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
            {
                return (true, null);
            }

            return (false, "The SCMTR file must contain a JSON object or array at the root.");
        }
        catch (JsonException ex)
        {
            return (false, $"The SCMTR file is not valid JSON: {ex.Message}");
        }
        finally
        {
            if (stream.CanSeek)
            {
                stream.Seek(0, SeekOrigin.Begin);
            }
        }
    }

    public static bool IsValidJson(string content, out string? error)
    {
        try
        {
            using var document = JsonDocument.Parse(content);
            if (document.RootElement.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
            {
                error = null;
                return true;
            }

            error = "The content must contain a JSON object or array at the root.";
            return false;
        }
        catch (JsonException ex)
        {
            error = $"Invalid JSON: {ex.Message}";
            return false;
        }
    }
}

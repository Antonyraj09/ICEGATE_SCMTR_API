using System.Text.Json.Serialization;

namespace Icegate.Integration.Models.Acknowledgement;

/// <summary>Request body for our internal ACK endpoint, POST /api/icegate/ack.</summary>
public class GetAckRequest
{
    [JsonPropertyName("senderId")]
    public string SenderId { get; set; } = string.Empty;

    [JsonPropertyName("uniqueId")]
    public string UniqueId { get; set; } = string.Empty;
}

/// <summary>Request body sent to the ICEGATE Get Acknowledgement API (application/json, header token: &lt;accessToken&gt;).</summary>
public class IcegateGetAckRequest
{
    [JsonPropertyName("senderId")]
    public string SenderId { get; set; } = string.Empty;

    [JsonPropertyName("uniqueId")]
    public string UniqueId { get; set; } = string.Empty;
}

/// <summary>
/// Fields extracted from the ICEGATE multipart Get Acknowledgement response:
/// senderId, uniqueId, responseDesc, plus the ACK file part (name + content).
/// </summary>
public class IcegateAckMultipartResult
{
    public string? SenderId { get; set; }

    public string? UniqueId { get; set; }

    public string? ResponseDesc { get; set; }

    public string? AckFileName { get; set; }

    public byte[]? AckFileContent { get; set; }

    /// <summary>True when ICEGATE indicates the ACK is not yet generated (no file part present).</summary>
    public bool IsAckAvailable => AckFileContent is { Length: > 0 } && !string.IsNullOrWhiteSpace(AckFileName);
}

/// <summary>Payload returned to the caller inside IcegateApiResult.Data for a successful ACK retrieval.</summary>
public class AckResultData
{
    [JsonPropertyName("senderId")]
    public string SenderId { get; set; } = string.Empty;

    [JsonPropertyName("uniqueId")]
    public string UniqueId { get; set; } = string.Empty;

    [JsonPropertyName("responseDesc")]
    public string? ResponseDesc { get; set; }

    [JsonPropertyName("fileName")]
    public string? FileName { get; set; }

    [JsonPropertyName("filePath")]
    public string? FilePath { get; set; }
}

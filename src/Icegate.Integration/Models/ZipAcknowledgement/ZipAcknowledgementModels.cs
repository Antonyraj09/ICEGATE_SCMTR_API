using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Icegate.Integration.Models.ZipAcknowledgement;

/// <summary>Request body for our internal ZIP ACK endpoint, POST /api/icegate/outbound/zip-ack.</summary>
public class GetZipAckRequest
{
    [JsonPropertyName("icegateId")]
    [Required]
    public string IcegateId { get; set; } = string.Empty;

    [JsonPropertyName("messageId")]
    [Required]
    public string MessageId { get; set; } = string.Empty;

    [JsonPropertyName("custodianCode")]
    [Required]
    public string CustodianCode { get; set; } = string.Empty;

    /// <summary>Allowed range per the document: 1 to 50.</summary>
    [JsonPropertyName("batchSize")]
    [Range(1, 50)]
    public int BatchSize { get; set; } = 50;
}

/// <summary>Request body sent to ICEGATE Get Zip Acknowledgement API (application/json, header token, query param batchSize).</summary>
public class IcegateGetZipAckRequest
{
    [JsonPropertyName("icegateId")]
    public string IcegateId { get; set; } = string.Empty;

    [JsonPropertyName("messageId")]
    public string MessageId { get; set; } = string.Empty;

    [JsonPropertyName("custodianCode")]
    public string CustodianCode { get; set; } = string.Empty;
}

/// <summary>
/// Fields extracted from the ICEGATE multipart Get Zip Acknowledgement response:
/// icegateId, messageId, custodianCode, responseId, fileCount, responseDesc, plus the ZIP file part.
/// </summary>
public class IcegateZipAckMultipartResult
{
    public string? IcegateId { get; set; }

    public string? MessageId { get; set; }

    public string? CustodianCode { get; set; }

    public string? ResponseId { get; set; }

    public int? FileCount { get; set; }

    public string? ResponseDesc { get; set; }

    public string? ZipFileName { get; set; }

    public byte[]? ZipFileContent { get; set; }

    public bool IsZipAvailable => ZipFileContent is { Length: > 0 } && !string.IsNullOrWhiteSpace(ZipFileName);
}

/// <summary>Payload returned to the caller inside IcegateApiResult.Data for a successful ZIP ACK retrieval.</summary>
public class ZipAckResultData
{
    [JsonPropertyName("icegateId")]
    public string IcegateId { get; set; } = string.Empty;

    [JsonPropertyName("messageId")]
    public string MessageId { get; set; } = string.Empty;

    [JsonPropertyName("custodianCode")]
    public string CustodianCode { get; set; } = string.Empty;

    [JsonPropertyName("responseId")]
    public string? ResponseId { get; set; }

    [JsonPropertyName("fileCount")]
    public int? FileCount { get; set; }

    [JsonPropertyName("responseDesc")]
    public string? ResponseDesc { get; set; }

    [JsonPropertyName("fileName")]
    public string? FileName { get; set; }

    [JsonPropertyName("filePath")]
    public string? FilePath { get; set; }
}

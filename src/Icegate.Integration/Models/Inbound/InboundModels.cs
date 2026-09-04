using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;

namespace Icegate.Integration.Models.Inbound;

/// <summary>
/// Multipart/form-data request accepted by our internal upload endpoint,
/// POST /api/icegate/inbound/upload.
/// </summary>
public class InboundUploadRequest
{
    /// <summary>The SCMTR JSON file, sent as-is to ICEGATE without modification.</summary>
    public IFormFile File { get; set; } = null!;

    public string? LocalReferenceNo { get; set; }

    public string? IcegateId { get; set; }

    public string? SenderId { get; set; }
}

/// <summary>
/// Response body documented by ICEGATE for the inbound upload API
/// (POST {InboundUploadUrl}, multipart/form-data, header token: &lt;accessToken&gt;).
/// </summary>
public class IcegateInboundUploadResponse
{
    [JsonPropertyName("validationStatus")]
    public string? ValidationStatus { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }

    [JsonPropertyName("uniqueId")]
    public string? UniqueId { get; set; }

    public bool IsSuccess =>
        string.Equals(ValidationStatus, "SUCCESS", StringComparison.OrdinalIgnoreCase);
}

/// <summary>Payload returned to the caller inside IcegateApiResult.Data for a successful upload.</summary>
public class InboundUploadResultData
{
    [JsonPropertyName("validationStatus")]
    public string ValidationStatus { get; set; } = string.Empty;

    [JsonPropertyName("uniqueId")]
    public string UniqueId { get; set; } = string.Empty;
}

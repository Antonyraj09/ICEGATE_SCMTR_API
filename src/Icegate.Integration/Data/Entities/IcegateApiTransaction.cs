using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Icegate.Integration.Models.Common;

namespace Icegate.Integration.Data.Entities;

/// <summary>
/// Persistent record of every ICEGATE SCMTR transaction. Deliberately excludes
/// secrets (API key, password, JWT/access token, encrypted authentication credentials) -
/// see rule "Do NOT store" in the integration specification.
/// </summary>
[Table("ICEGATE_API_TRANSACTION")]
public class IcegateApiTransaction
{
    [Key]
    public long Id { get; set; }

    [MaxLength(100)]
    public string? LocalReferenceNo { get; set; }

    [MaxLength(500)]
    public string? FileName { get; set; }

    [MaxLength(128)]
    public string? FileHash { get; set; }

    public IcegateApiType ApiType { get; set; }

    /// <summary>"UAT" or "PROD" - the environment this transaction was executed against.</summary>
    [MaxLength(10)]
    public string Environment { get; set; } = string.Empty;

    public DateTime RequestDateTime { get; set; }

    public DateTime? ResponseDateTime { get; set; }

    [MaxLength(100)]
    public string? IcegateUniqueId { get; set; }

    [MaxLength(100)]
    public string? MessageId { get; set; }

    [MaxLength(100)]
    public string? SenderId { get; set; }

    [MaxLength(100)]
    public string? IcegateId { get; set; }

    [MaxLength(100)]
    public string? CustodianCode { get; set; }

    public TransactionStatus Status { get; set; }

    [MaxLength(50)]
    public string? ValidationStatus { get; set; }

    [MaxLength(1000)]
    public string? ResponseDescription { get; set; }

    [MaxLength(2000)]
    public string? ErrorMessage { get; set; }

    [MaxLength(500)]
    public string? AckFileName { get; set; }

    [MaxLength(1000)]
    public string? AckFilePath { get; set; }

    [MaxLength(500)]
    public string? ZipFileName { get; set; }

    [MaxLength(1000)]
    public string? ZipFilePath { get; set; }

    [MaxLength(50)]
    public string CorrelationId { get; set; } = string.Empty;

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedDate { get; set; }
}

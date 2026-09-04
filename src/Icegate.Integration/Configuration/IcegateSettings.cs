namespace Icegate.Integration.Configuration;

/// <summary>
/// Strongly typed binding of the "Icegate" configuration section.
/// Values are environment-specific (UAT / PROD) and must never contain
/// hard-coded secrets in source control. Any field whose value equals
/// "CONFIRM_WITH_ICEGATE" is a placeholder pending confirmation from the
/// ICEGATE team per the API Contract Document (SCMTR Open API Filing v1.6).
/// </summary>
public class IcegateSettings
{
    public const string SectionName = "Icegate";

    public const string ConfirmWithIcegatePlaceholder = "CONFIRM_WITH_ICEGATE";

    /// <summary>"UAT" or "PROD". Drives which URLs/credentials are considered active.</summary>
    public string Environment { get; set; } = "UAT";

    public string AuthenticationUrl { get; set; } = string.Empty;

    public string InboundUploadUrl { get; set; } = string.Empty;

    /// <summary>
    /// The document indicates this endpoint must be rechecked with ICEGATE before
    /// integration testing / go-live. Defaults to CONFIRM_WITH_ICEGATE until the
    /// ICEGATE team confirms it, per document section on Get Acknowledgement.
    /// </summary>
    public string GetAcknowledgementUrl { get; set; } = ConfirmWithIcegatePlaceholder;

    public string GetZipAcknowledgementUrl { get; set; } = string.Empty;

    /// <summary>ICEGATE-issued API key / subscription key, supplied via secret storage, never source control.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Encrypted credential payload (or the material used to build it) for the Authentication API "data" field.</summary>
    public string EncryptedCredentialData { get; set; } = string.Empty;

    public string IcegateId { get; set; } = string.Empty;

    public string SenderId { get; set; } = string.Empty;

    public string CustodianCode { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 120;

    /// <summary>Safety buffer (seconds) subtracted from the ~15 minute token lifetime before it is considered expired.</summary>
    public int TokenSafetyBufferSeconds { get; set; } = 60;

    /// <summary>Nominal ICEGATE token lifetime in seconds, used only when the token response omits an explicit expiry (document states ~15 minutes).</summary>
    public int TokenNominalLifetimeSeconds { get; set; } = 900;

    /// <summary>Number of controlled retries for transient network/timeout failures (not business validation failures).</summary>
    public int NetworkRetryCount { get; set; } = 2;

    public int MinBatchSize { get; set; } = 1;

    public int MaxBatchSize { get; set; } = 50;

    public bool IsConfirmed(string? url) =>
        !string.IsNullOrWhiteSpace(url) &&
        !string.Equals(url, ConfirmWithIcegatePlaceholder, StringComparison.OrdinalIgnoreCase);
}

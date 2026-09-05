namespace Icegate.Integration.Configuration;

/// <summary>
/// One onboarded client (a distinct consuming application/business entity), each with its
/// own ICEGATE registration - own encrypted credential payload, own sender/ICEGATE/custodian
/// identifiers - and its own internal API key to authenticate against OUR API. Multiple
/// clients share the same ICEGATE endpoints/environment (those stay in <see cref="IcegateSettings"/>)
/// but never share credentials, tokens, or transaction data.
/// </summary>
public class IcegateClientSettings
{
    /// <summary>The configuration section holding the array of onboarded clients (bound directly to List&lt;IcegateClientSettings&gt;).</summary>
    public const string SectionName = "IcegateClients";

    /// <summary>Stable internal identifier for this client, e.g. "ACME-CFS". Used in logs and transaction rows.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>The internal API key this client authenticates to OUR API with (X-API-KEY). Unique per client.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>This client's own ICEGATE encrypted credential payload for the Authentication API's "data" field.</summary>
    public string EncryptedCredentialData { get; set; } = string.Empty;

    /// <summary>This client's ICEGATE-issued API key/subscription key, if ICEGATE requires one per filer.</summary>
    public string IcegateApiKey { get; set; } = string.Empty;

    /// <summary>Used as the sender ID when a request does not explicitly supply one.</summary>
    public string DefaultSenderId { get; set; } = string.Empty;

    /// <summary>Used as the ICEGATE ID when a request does not explicitly supply one.</summary>
    public string DefaultIcegateId { get; set; } = string.Empty;

    /// <summary>Used as the custodian code when a request does not explicitly supply one.</summary>
    public string DefaultCustodianCode { get; set; } = string.Empty;

    /// <summary>Set to false to suspend a client without deleting its configuration/history.</summary>
    public bool Enabled { get; set; } = true;
}

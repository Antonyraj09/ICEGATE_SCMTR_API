using System.Text.Json.Serialization;

namespace Icegate.Integration.Models.Authentication;

/// <summary>
/// Request body for the ICEGATE Authentication API.
/// POST {AuthenticationUrl}, Content-Type: application/json.
/// </summary>
public class IcegateAuthenticationRequest
{
    /// <summary>Encrypted credential payload required by ICEGATE. Never a plaintext username/password.</summary>
    [JsonPropertyName("data")]
    public string Data { get; set; } = string.Empty;
}

/// <summary>
/// Raw response from the ICEGATE Authentication API. The document does not enumerate every
/// field ICEGATE may return alongside the token, so unknown fields are tolerated via
/// <see cref="System.Text.Json.JsonExtensionData"/> rather than dropped or guessed at.
/// </summary>
public class IcegateAuthenticationResponse
{
    [JsonPropertyName("token")]
    public string? Token { get; set; }

    /// <summary>Some ICEGATE deployments return "access_token" instead of "token" - both are honored.</summary>
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }

    /// <summary>Optional explicit expiry (seconds) if ICEGATE returns one; otherwise the nominal ~15 minute lifetime from the document is used.</summary>
    [JsonPropertyName("expiresIn")]
    public int? ExpiresInSeconds { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonExtensionData]
    public Dictionary<string, System.Text.Json.JsonElement>? Extra { get; set; }

    public string? ResolveToken() => !string.IsNullOrWhiteSpace(Token) ? Token : AccessToken;
}

/// <summary>In-memory representation of a cached ICEGATE token and its lifecycle.</summary>
public class IcegateCachedToken
{
    public required string Token { get; init; }

    public DateTimeOffset IssuedAtUtc { get; init; }

    public DateTimeOffset ExpiresAtUtc { get; init; }

    public bool IsUsable(TimeSpan safetyBuffer) =>
        !string.IsNullOrWhiteSpace(Token) && DateTimeOffset.UtcNow < ExpiresAtUtc - safetyBuffer;
}

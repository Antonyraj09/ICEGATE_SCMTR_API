using System.Text.Json.Serialization;

namespace Icegate.Integration.Models.Common;

/// <summary>
/// Standard response envelope returned by every endpoint on our .NET 8 API,
/// as required by the API response standard (see Developer Integration Guide).
/// </summary>
/// <typeparam name="T">Payload type.</typeparam>
public class IcegateApiResult<T>
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("statusCode")]
    public int StatusCode { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }

    [JsonPropertyName("correlationId")]
    public string CorrelationId { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public T? Data { get; set; }

    public static IcegateApiResult<T> Ok(T data, string message, string correlationId, int statusCode = 200) =>
        new()
        {
            Success = true,
            StatusCode = statusCode,
            Message = message,
            ErrorMessage = null,
            CorrelationId = correlationId,
            Data = data
        };

    public static IcegateApiResult<T> Fail(string message, string? errorMessage, string correlationId, int statusCode) =>
        new()
        {
            Success = false,
            StatusCode = statusCode,
            Message = message,
            ErrorMessage = errorMessage,
            CorrelationId = correlationId,
            Data = default
        };
}

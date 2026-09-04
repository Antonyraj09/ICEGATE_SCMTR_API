namespace Icegate.Integration.Models.Common;

/// <summary>
/// Verbatim ICEGATE business/validation error messages as they appear in the
/// API Contract Document - SCMTR Open API Filing v1.6. These are preserved exactly when
/// surfaced to callers via <see cref="IcegateApiResult{T}.ErrorMessage"/> - never replaced
/// with a generic message. Consumers may layer a friendlier <c>Message</c> alongside them.
/// </summary>
public static class IcegateDocumentedErrors
{
    public const string AuthorizationFailedSender = "Authorization failed, request not allowed for provided sender ID.";
    public const string TokenMissing = "The required authorization token is missing from the request headers.";
    public const string TokenInvalidOrExpired = "The provided token is invalid or has expired.";
    public const string SenderIdRequired = "The sender ID is required and must not be blank.";
    public const string ReceiverIdRequired = "The receiver ID is required and must not be blank.";
    public const string MessageIdRequired = "The message ID is required and must not be blank.";
    public const string CustodianCodeNotAllowed = "The provided custodian code is not allowed for API.";
    public const string MessageIdNotAllowed = "The provided message ID is not allowed for API.";
    public const string CustodianCodeNotMapped = "Custodian code not mapped to ICEGATE ID or Invalid Custodian code.";
    public const string UnexpectedError = "An unexpected error occurred. Please try again later or contact support if the issue persists.";
}

/// <summary>Internal error codes used in logs/responses to categorize failures without exposing internals.</summary>
public static class IcegateErrorCode
{
    public const string InvalidFile = "INVALID_FILE";
    public const string InvalidJson = "INVALID_JSON";
    public const string EmptyFile = "EMPTY_FILE";
    public const string FileTooLarge = "FILE_TOO_LARGE";
    public const string InvalidExtension = "INVALID_EXTENSION";
    public const string TokenMissing = "TOKEN_MISSING";
    public const string TokenInvalidOrExpired = "TOKEN_INVALID_OR_EXPIRED";
    public const string InvalidApiKey = "INVALID_API_KEY";
    public const string MissingSenderId = "MISSING_SENDER_ID";
    public const string InvalidSenderId = "INVALID_SENDER_ID";
    public const string MissingReceiverId = "MISSING_RECEIVER_ID";
    public const string InvalidReceiverId = "INVALID_RECEIVER_ID";
    public const string MissingMessageId = "MISSING_MESSAGE_ID";
    public const string InvalidMessageId = "INVALID_MESSAGE_ID";
    public const string InvalidCustodianCode = "INVALID_CUSTODIAN_CODE";
    public const string UnauthorizedSender = "UNAUTHORIZED_SENDER";
    public const string UnauthorizedMessageId = "UNAUTHORIZED_MESSAGE_ID";
    public const string NetworkFailure = "NETWORK_FAILURE";
    public const string Timeout = "TIMEOUT";
    public const string MalformedMultipart = "MALFORMED_MULTIPART";
    public const string AckNotYetAvailable = "ACK_NOT_YET_AVAILABLE";
    public const string MissingAckFile = "MISSING_ACK_FILE";
    public const string IcegateUnavailable = "ICEGATE_UNAVAILABLE";
    public const string BusinessValidationFailed = "BUSINESS_VALIDATION_FAILED";
    public const string InvalidBatchSize = "INVALID_BATCH_SIZE";
    public const string UnknownTransaction = "UNKNOWN_TRANSACTION";
    public const string InternalError = "INTERNAL_ERROR";
    public const string EndpointNotConfirmed = "ENDPOINT_NOT_CONFIRMED";
}

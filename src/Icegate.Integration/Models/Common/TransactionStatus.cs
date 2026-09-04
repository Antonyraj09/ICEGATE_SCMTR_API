namespace Icegate.Integration.Models.Common;

/// <summary>
/// Internal lifecycle status for an ICEGATE_API_TRANSACTION row.
/// HTTP 200 on the upload call is NOT sufficient to consider a transaction complete -
/// the ICEGATE business validation response and subsequent acknowledgement must be honored.
/// </summary>
public enum TransactionStatus
{
    CREATED = 0,
    SUBMITTED = 1,
    SUBMISSION_FAILED = 2,
    PROCESSING = 3,
    ACK_PENDING = 4,
    ACK_RECEIVED = 5,
    ACK_FAILED = 6,
    ZIP_ACK_RECEIVED = 7,
    FAILED = 8
}

/// <summary>Distinguishes which ICEGATE API a transaction row/log entry relates to.</summary>
public enum IcegateApiType
{
    AUTHENTICATION = 0,
    INBOUND_UPLOAD = 1,
    GET_ACK = 2,
    GET_ZIP_ACK = 3
}

using Icegate.Integration.Data.Entities;
using Icegate.Integration.Models.Common;

namespace Icegate.Integration.Services.Interfaces;

/// <summary>Persists and updates ICEGATE_API_TRANSACTION rows. Never accepts secrets as input.</summary>
public interface ITransactionLogService
{
    Task<IcegateApiTransaction> CreateAsync(
        string clientId,
        IcegateApiType apiType,
        string correlationId,
        string? localReferenceNo,
        string? fileName,
        string? fileHash,
        string? senderId,
        string? icegateId,
        string? custodianCode,
        string? messageId,
        TransactionStatus status,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        long transactionId,
        TransactionStatus status,
        string? validationStatus = null,
        string? responseDescription = null,
        string? errorMessage = null,
        string? icegateUniqueId = null,
        string? ackFileName = null,
        string? ackFilePath = null,
        string? zipFileName = null,
        string? zipFilePath = null,
        CancellationToken cancellationToken = default);

    /// <summary>Scoped to a single client so one client can never look up another client's transaction by guessing a uniqueId.</summary>
    Task<IcegateApiTransaction?> FindLatestByUniqueIdAsync(string clientId, string uniqueId, CancellationToken cancellationToken = default);
}

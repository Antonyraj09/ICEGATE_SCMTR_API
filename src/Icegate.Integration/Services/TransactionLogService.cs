using Icegate.Integration.Configuration;
using Icegate.Integration.Data;
using Icegate.Integration.Data.Entities;
using Icegate.Integration.Models.Common;
using Icegate.Integration.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Icegate.Integration.Services;

public class TransactionLogService : ITransactionLogService
{
    private readonly IcegateDbContext _dbContext;
    private readonly IOptionsMonitor<IcegateSettings> _settings;

    public TransactionLogService(IcegateDbContext dbContext, IOptionsMonitor<IcegateSettings> settings)
    {
        _dbContext = dbContext;
        _settings = settings;
    }

    public async Task<IcegateApiTransaction> CreateAsync(
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
        CancellationToken cancellationToken = default)
    {
        var entity = new IcegateApiTransaction
        {
            ApiType = apiType,
            Environment = _settings.CurrentValue.Environment,
            CorrelationId = correlationId,
            LocalReferenceNo = localReferenceNo,
            FileName = fileName,
            FileHash = fileHash,
            SenderId = senderId,
            IcegateId = icegateId,
            CustodianCode = custodianCode,
            MessageId = messageId,
            Status = status,
            RequestDateTime = DateTime.UtcNow,
            CreatedDate = DateTime.UtcNow
        };

        _dbContext.Transactions.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(
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
        CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Transactions.FirstOrDefaultAsync(t => t.Id == transactionId, cancellationToken);
        if (entity is null)
        {
            return;
        }

        entity.Status = status;
        entity.ResponseDateTime = DateTime.UtcNow;
        entity.UpdatedDate = DateTime.UtcNow;

        if (validationStatus is not null) entity.ValidationStatus = validationStatus;
        if (responseDescription is not null) entity.ResponseDescription = Truncate(responseDescription, 1000);
        if (errorMessage is not null) entity.ErrorMessage = Truncate(errorMessage, 2000);
        if (icegateUniqueId is not null) entity.IcegateUniqueId = icegateUniqueId;
        if (ackFileName is not null) entity.AckFileName = ackFileName;
        if (ackFilePath is not null) entity.AckFilePath = ackFilePath;
        if (zipFileName is not null) entity.ZipFileName = zipFileName;
        if (zipFilePath is not null) entity.ZipFilePath = zipFilePath;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IcegateApiTransaction?> FindLatestByUniqueIdAsync(string uniqueId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Transactions
            .Where(t => t.IcegateUniqueId == uniqueId)
            .OrderByDescending(t => t.CreatedDate)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}

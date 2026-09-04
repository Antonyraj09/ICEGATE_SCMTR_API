using Icegate.Integration.Configuration;
using Icegate.Integration.Helpers;
using Icegate.Integration.Http;
using Icegate.Integration.Models.Common;
using Icegate.Integration.Models.ZipAcknowledgement;
using Icegate.Integration.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace Icegate.Integration.Services;

/// <summary>
/// Calls the ICEGATE Get Zip Acknowledgement API (POST {GetZipAcknowledgementUrl}?batchSize=,
/// application/json, header token) and parses its multipart response (icegateId, messageId,
/// custodianCode, responseId, fileCount, responseDesc, plus the ZIP file part).
/// </summary>
public class IcegateZipAcknowledgementService : IIcegateZipAcknowledgementService
{
    private const string TokenHeaderName = "token";

    private readonly IIcegateHttpClient _httpClient;
    private readonly ITokenRetryExecutor _retryExecutor;
    private readonly ITransactionLogService _transactionLog;
    private readonly IOptionsMonitor<IcegateSettings> _icegateSettings;
    private readonly IOptionsMonitor<FileStorageSettings> _storageSettings;
    private readonly ILogger<IcegateZipAcknowledgementService> _logger;

    public IcegateZipAcknowledgementService(
        IIcegateHttpClient httpClient,
        ITokenRetryExecutor retryExecutor,
        ITransactionLogService transactionLog,
        IOptionsMonitor<IcegateSettings> icegateSettings,
        IOptionsMonitor<FileStorageSettings> storageSettings,
        ILogger<IcegateZipAcknowledgementService> logger)
    {
        _httpClient = httpClient;
        _retryExecutor = retryExecutor;
        _transactionLog = transactionLog;
        _icegateSettings = icegateSettings;
        _storageSettings = storageSettings;
        _logger = logger;
    }

    public async Task<ZipAckResultData> GetZipAcknowledgementAsync(
        GetZipAckRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var icegateSettings = _icegateSettings.CurrentValue;

        ValidateRequest(request, icegateSettings);

        if (string.IsNullOrWhiteSpace(icegateSettings.GetZipAcknowledgementUrl))
        {
            throw new IcegateEndpointNotConfirmedException(nameof(icegateSettings.GetZipAcknowledgementUrl));
        }

        var transaction = await _transactionLog.CreateAsync(
            IcegateApiType.GET_ZIP_ACK,
            correlationId,
            localReferenceNo: null,
            fileName: null,
            fileHash: null,
            senderId: null,
            icegateId: request.IcegateId,
            custodianCode: request.CustodianCode,
            messageId: request.MessageId,
            TransactionStatus.PROCESSING,
            cancellationToken);

        try
        {
            var requestBody = new IcegateGetZipAckRequest
            {
                IcegateId = request.IcegateId,
                MessageId = request.MessageId,
                CustodianCode = request.CustodianCode
            };

            var url = $"{icegateSettings.GetZipAcknowledgementUrl}?batchSize={request.BatchSize}";

            using var response = await _retryExecutor.ExecuteAsync(
                (token, ct) => _httpClient.PostJsonAsync(
                    url,
                    requestBody,
                    new Dictionary<string, string> { [TokenHeaderName] = token },
                    ct),
                cancellationToken);

            var parts = await MultipartResponseParser.ParseAsync(response.Content, cancellationToken);

            var zipResult = new IcegateZipAckMultipartResult
            {
                IcegateId = MultipartResponseParser.FindField(parts, "icegateId") ?? request.IcegateId,
                MessageId = MultipartResponseParser.FindField(parts, "messageId") ?? request.MessageId,
                CustodianCode = MultipartResponseParser.FindField(parts, "custodianCode") ?? request.CustodianCode,
                ResponseId = MultipartResponseParser.FindField(parts, "responseId"),
                ResponseDesc = MultipartResponseParser.FindField(parts, "responseDesc", "responseDescription")
            };

            var fileCountRaw = MultipartResponseParser.FindField(parts, "fileCount");
            if (int.TryParse(fileCountRaw, out var fileCount))
            {
                zipResult.FileCount = fileCount;
            }

            var filePart = MultipartResponseParser.FindFilePart(parts);
            if (filePart is not null)
            {
                zipResult.ZipFileName = filePart.FileName;
                zipResult.ZipFileContent = filePart.Content;
            }

            if (!zipResult.IsZipAvailable)
            {
                await _transactionLog.UpdateAsync(
                    transaction.Id,
                    TransactionStatus.FAILED,
                    responseDescription: zipResult.ResponseDesc,
                    errorMessage: "ICEGATE did not return a ZIP file part in the response.",
                    cancellationToken: cancellationToken);

                throw new IcegateMultipartParseException("ICEGATE Get Zip Acknowledgement response did not contain a ZIP file part.");
            }

            var stored = await FileStorageHelper.SaveAsync(
                _storageSettings.CurrentValue.ZipAckPath,
                zipResult.ZipFileName!,
                zipResult.ZipFileContent!,
                cancellationToken);

            await _transactionLog.UpdateAsync(
                transaction.Id,
                TransactionStatus.ZIP_ACK_RECEIVED,
                responseDescription: zipResult.ResponseDesc,
                zipFileName: stored.GeneratedFileName,
                zipFilePath: stored.FilePath,
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "ZIP ACK retrieved for CorrelationId={CorrelationId} MessageId={MessageId} FileCount={FileCount}",
                correlationId, request.MessageId, zipResult.FileCount);

            return new ZipAckResultData
            {
                IcegateId = zipResult.IcegateId ?? request.IcegateId,
                MessageId = zipResult.MessageId ?? request.MessageId,
                CustodianCode = zipResult.CustodianCode ?? request.CustodianCode,
                ResponseId = zipResult.ResponseId,
                FileCount = zipResult.FileCount,
                ResponseDesc = zipResult.ResponseDesc,
                FileName = stored.GeneratedFileName,
                FilePath = stored.FilePath
            };
        }
        catch (IcegateIntegrationException ex)
        {
            await _transactionLog.UpdateAsync(
                transaction.Id,
                TransactionStatus.FAILED,
                errorMessage: ex.Message,
                cancellationToken: cancellationToken);
            throw;
        }
    }

    private static void ValidateRequest(GetZipAckRequest request, IcegateSettings settings)
    {
        if (string.IsNullOrWhiteSpace(request.IcegateId))
        {
            throw new IcegateRequestValidationException(IcegateErrorCode.MissingSenderId, IcegateDocumentedErrors.SenderIdRequired);
        }

        if (string.IsNullOrWhiteSpace(request.MessageId))
        {
            throw new IcegateRequestValidationException(IcegateErrorCode.MissingMessageId, IcegateDocumentedErrors.MessageIdRequired);
        }

        if (string.IsNullOrWhiteSpace(request.CustodianCode))
        {
            throw new IcegateRequestValidationException(IcegateErrorCode.InvalidCustodianCode, IcegateDocumentedErrors.CustodianCodeNotMapped);
        }

        if (request.BatchSize < settings.MinBatchSize || request.BatchSize > settings.MaxBatchSize)
        {
            throw new IcegateRequestValidationException(
                IcegateErrorCode.InvalidBatchSize,
                $"batchSize must be between {settings.MinBatchSize} and {settings.MaxBatchSize}.");
        }
    }
}

using System.Net.Http.Json;
using Icegate.Integration.Configuration;
using Icegate.Integration.Helpers;
using Icegate.Integration.Http;
using Icegate.Integration.Models.Common;
using Icegate.Integration.Models.Inbound;
using Icegate.Integration.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Icegate.Integration.Services;

/// <summary>
/// Submits an SCMTR JSON file to the ICEGATE inbound upload API
/// (POST {InboundUploadUrl}, multipart/form-data, header token: &lt;accessToken&gt;),
/// capturing the returned uniqueId and logging the transaction. Never renames SCMTR
/// properties or alters the file content - the original bytes are sent as-is.
/// </summary>
public class IcegateFileSubmissionService : IIcegateFileSubmissionService
{
    private const string TokenHeaderName = "token";

    private readonly IIcegateHttpClient _httpClient;
    private readonly ITokenRetryExecutor _retryExecutor;
    private readonly ITransactionLogService _transactionLog;
    private readonly IOptionsMonitor<IcegateSettings> _icegateSettings;
    private readonly IOptionsMonitor<FileStorageSettings> _storageSettings;
    private readonly ILogger<IcegateFileSubmissionService> _logger;

    public IcegateFileSubmissionService(
        IIcegateHttpClient httpClient,
        ITokenRetryExecutor retryExecutor,
        ITransactionLogService transactionLog,
        IOptionsMonitor<IcegateSettings> icegateSettings,
        IOptionsMonitor<FileStorageSettings> storageSettings,
        ILogger<IcegateFileSubmissionService> logger)
    {
        _httpClient = httpClient;
        _retryExecutor = retryExecutor;
        _transactionLog = transactionLog;
        _icegateSettings = icegateSettings;
        _storageSettings = storageSettings;
        _logger = logger;
    }

    public async Task<InboundUploadResultData> SubmitAsync(
        IFormFile file,
        string? localReferenceNo,
        string? icegateId,
        string? senderId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var storageSettings = _storageSettings.CurrentValue;
        var icegateSettings = _icegateSettings.CurrentValue;

        var (isValid, errorCode, error) = await FileValidationHelper.ValidateAsync(file, storageSettings, cancellationToken);
        if (!isValid)
        {
            throw new IcegateRequestValidationException(errorCode, error ?? "The uploaded file failed validation.");
        }

        if (string.IsNullOrWhiteSpace(icegateSettings.InboundUploadUrl))
        {
            throw new IcegateEndpointNotConfirmedException(nameof(icegateSettings.InboundUploadUrl));
        }

        await using var readStream = file.OpenReadStream();
        var fileBytes = await ReadAllBytesAsync(readStream, cancellationToken);
        var fileHash = await FileStorageHelper.ComputeHashAsync(new MemoryStream(fileBytes), cancellationToken);

        var stored = await FileStorageHelper.SaveAsync(storageSettings.InboundPath, file.FileName, fileBytes, cancellationToken);

        var transaction = await _transactionLog.CreateAsync(
            IcegateApiType.INBOUND_UPLOAD,
            correlationId,
            localReferenceNo,
            stored.GeneratedFileName,
            fileHash,
            senderId,
            icegateId,
            custodianCode: null,
            messageId: null,
            TransactionStatus.CREATED,
            cancellationToken);

        try
        {
            using var response = await _retryExecutor.ExecuteAsync(
                (token, ct) => _httpClient.PostMultipartAsync(
                    icegateSettings.InboundUploadUrl,
                    BuildMultipartContent(fileBytes, file.FileName),
                    new Dictionary<string, string> { [TokenHeaderName] = token },
                    ct),
                cancellationToken);

            var icegateResponse = await response.Content.ReadFromJsonAsync<IcegateInboundUploadResponse>(cancellationToken: cancellationToken)
                ?? throw new IcegateBusinessValidationException(IcegateErrorCode.BusinessValidationFailed, IcegateDocumentedErrors.UnexpectedError);

            if (!icegateResponse.IsSuccess)
            {
                var errorMessage = icegateResponse.ErrorMessage ?? icegateResponse.Message ?? IcegateDocumentedErrors.UnexpectedError;

                await _transactionLog.UpdateAsync(
                    transaction.Id,
                    TransactionStatus.SUBMISSION_FAILED,
                    validationStatus: icegateResponse.ValidationStatus,
                    responseDescription: icegateResponse.Message,
                    errorMessage: errorMessage,
                    cancellationToken: cancellationToken);

                throw new IcegateBusinessValidationException(IcegateErrorCode.BusinessValidationFailed, errorMessage);
            }

            await _transactionLog.UpdateAsync(
                transaction.Id,
                TransactionStatus.SUBMITTED,
                validationStatus: icegateResponse.ValidationStatus,
                responseDescription: icegateResponse.Message,
                icegateUniqueId: icegateResponse.UniqueId,
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "SCMTR file submitted to ICEGATE. CorrelationId={CorrelationId} UniqueId={UniqueId}",
                correlationId, icegateResponse.UniqueId);

            return new InboundUploadResultData
            {
                ValidationStatus = icegateResponse.ValidationStatus ?? "SUCCESS",
                UniqueId = icegateResponse.UniqueId ?? string.Empty
            };
        }
        catch (IcegateIntegrationException ex) when (ex is not IcegateBusinessValidationException)
        {
            await _transactionLog.UpdateAsync(
                transaction.Id,
                TransactionStatus.SUBMISSION_FAILED,
                errorMessage: ex.Message,
                cancellationToken: cancellationToken);
            throw;
        }
    }

    private static MultipartFormDataContent BuildMultipartContent(byte[] fileBytes, string fileName)
    {
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        content.Add(fileContent, "file", fileName);
        return content;
    }

    private static async Task<byte[]> ReadAllBytesAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream, cancellationToken);
        return memoryStream.ToArray();
    }
}

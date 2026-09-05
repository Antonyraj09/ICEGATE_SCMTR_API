using Icegate.Integration.Configuration;
using Icegate.Integration.Helpers;
using Icegate.Integration.Http;
using Icegate.Integration.Models.Acknowledgement;
using Icegate.Integration.Models.Common;
using Icegate.Integration.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace Icegate.Integration.Services;

/// <summary>
/// Calls the ICEGATE Get Acknowledgement API and parses its multipart response (senderId,
/// uniqueId, responseDesc, plus the ACK file part). The Get Acknowledgement URL is marked
/// CONFIRM_WITH_ICEGATE in configuration per the document until ICEGATE confirms it.
/// </summary>
public class IcegateAcknowledgementService : IIcegateAcknowledgementService
{
    private const string TokenHeaderName = "token";

    private readonly IIcegateHttpClient _httpClient;
    private readonly ITokenRetryExecutor _retryExecutor;
    private readonly ITransactionLogService _transactionLog;
    private readonly IIcegateClientRegistry _clientRegistry;
    private readonly IOptionsMonitor<IcegateSettings> _icegateSettings;
    private readonly IOptionsMonitor<FileStorageSettings> _storageSettings;
    private readonly ILogger<IcegateAcknowledgementService> _logger;

    public IcegateAcknowledgementService(
        IIcegateHttpClient httpClient,
        ITokenRetryExecutor retryExecutor,
        ITransactionLogService transactionLog,
        IIcegateClientRegistry clientRegistry,
        IOptionsMonitor<IcegateSettings> icegateSettings,
        IOptionsMonitor<FileStorageSettings> storageSettings,
        ILogger<IcegateAcknowledgementService> logger)
    {
        _httpClient = httpClient;
        _retryExecutor = retryExecutor;
        _transactionLog = transactionLog;
        _clientRegistry = clientRegistry;
        _icegateSettings = icegateSettings;
        _storageSettings = storageSettings;
        _logger = logger;
    }

    public async Task<AckResultData> GetAcknowledgementAsync(
        string clientId,
        string senderId,
        string uniqueId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        if (!_clientRegistry.TryGetByClientId(clientId, out var client) || client is null)
        {
            throw new IcegateRequestValidationException(IcegateErrorCode.InvalidApiKey, $"No enabled client is registered for ClientId '{clientId}'.");
        }

        if (string.IsNullOrWhiteSpace(senderId))
        {
            senderId = client.DefaultSenderId;
        }

        if (string.IsNullOrWhiteSpace(senderId))
        {
            throw new IcegateRequestValidationException(IcegateErrorCode.MissingSenderId, IcegateDocumentedErrors.SenderIdRequired);
        }

        if (string.IsNullOrWhiteSpace(uniqueId))
        {
            throw new IcegateRequestValidationException(IcegateErrorCode.MissingMessageId, "The uniqueId is required and must not be blank.");
        }

        var icegateSettings = _icegateSettings.CurrentValue;

        if (!icegateSettings.IsConfirmed(icegateSettings.GetAcknowledgementUrl))
        {
            throw new IcegateEndpointNotConfirmedException(nameof(icegateSettings.GetAcknowledgementUrl));
        }

        var transaction = await _transactionLog.CreateAsync(
            clientId,
            IcegateApiType.GET_ACK,
            correlationId,
            localReferenceNo: null,
            fileName: null,
            fileHash: null,
            senderId,
            icegateId: null,
            custodianCode: null,
            messageId: uniqueId,
            TransactionStatus.ACK_PENDING,
            cancellationToken);

        try
        {
            var requestBody = new IcegateGetAckRequest { SenderId = senderId, UniqueId = uniqueId };

            using var response = await _retryExecutor.ExecuteAsync(
                clientId,
                (token, ct) => _httpClient.PostJsonAsync(
                    icegateSettings.GetAcknowledgementUrl,
                    requestBody,
                    new Dictionary<string, string> { [TokenHeaderName] = token },
                    ct),
                cancellationToken);

            var parts = await MultipartResponseParser.ParseAsync(response.Content, cancellationToken);

            var ackResult = new Models.Acknowledgement.IcegateAckMultipartResult
            {
                SenderId = MultipartResponseParser.FindField(parts, "senderId") ?? senderId,
                UniqueId = MultipartResponseParser.FindField(parts, "uniqueId") ?? uniqueId,
                ResponseDesc = MultipartResponseParser.FindField(parts, "responseDesc", "responseDescription")
            };

            var filePart = MultipartResponseParser.FindFilePart(parts);
            if (filePart is not null)
            {
                ackResult.AckFileName = filePart.FileName;
                ackResult.AckFileContent = filePart.Content;
            }

            if (!ackResult.IsAckAvailable)
            {
                await _transactionLog.UpdateAsync(
                    transaction.Id,
                    TransactionStatus.ACK_PENDING,
                    responseDescription: ackResult.ResponseDesc,
                    cancellationToken: cancellationToken);

                throw new IcegateAckNotYetAvailableException(
                    ackResult.ResponseDesc ?? "The acknowledgement has not yet been generated by ICEGATE. Please retry later.");
            }

            var stored = await FileStorageHelper.SaveAsync(
                _storageSettings.CurrentValue.AckPath,
                ackResult.AckFileName!,
                ackResult.AckFileContent!,
                cancellationToken);

            await _transactionLog.UpdateAsync(
                transaction.Id,
                TransactionStatus.ACK_RECEIVED,
                responseDescription: ackResult.ResponseDesc,
                icegateUniqueId: ackResult.UniqueId,
                ackFileName: stored.GeneratedFileName,
                ackFilePath: stored.FilePath,
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "ACK retrieved. ClientId={ClientId} CorrelationId={CorrelationId} UniqueId={UniqueId}",
                clientId, correlationId, uniqueId);

            return new AckResultData
            {
                SenderId = ackResult.SenderId ?? senderId,
                UniqueId = ackResult.UniqueId ?? uniqueId,
                ResponseDesc = ackResult.ResponseDesc,
                FileName = stored.GeneratedFileName,
                FilePath = stored.FilePath
            };
        }
        catch (IcegateAckNotYetAvailableException)
        {
            throw;
        }
        catch (IcegateIntegrationException ex)
        {
            await _transactionLog.UpdateAsync(
                transaction.Id,
                TransactionStatus.ACK_FAILED,
                errorMessage: ex.Message,
                cancellationToken: cancellationToken);
            throw;
        }
    }
}

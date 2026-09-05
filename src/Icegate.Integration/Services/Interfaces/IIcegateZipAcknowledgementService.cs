using Icegate.Integration.Models.ZipAcknowledgement;

namespace Icegate.Integration.Services.Interfaces;

public interface IIcegateZipAcknowledgementService
{
    Task<ZipAckResultData> GetZipAcknowledgementAsync(
        string clientId,
        GetZipAckRequest request,
        string correlationId,
        CancellationToken cancellationToken = default);
}

using Icegate.Integration.Models.ZipAcknowledgement;

namespace Icegate.Integration.Services.Interfaces;

public interface IIcegateZipAcknowledgementService
{
    Task<ZipAckResultData> GetZipAcknowledgementAsync(
        GetZipAckRequest request,
        string correlationId,
        CancellationToken cancellationToken = default);
}

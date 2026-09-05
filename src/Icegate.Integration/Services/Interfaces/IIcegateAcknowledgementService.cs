using Icegate.Integration.Models.Acknowledgement;

namespace Icegate.Integration.Services.Interfaces;

public interface IIcegateAcknowledgementService
{
    Task<AckResultData> GetAcknowledgementAsync(
        string clientId,
        string senderId,
        string uniqueId,
        string correlationId,
        CancellationToken cancellationToken = default);
}

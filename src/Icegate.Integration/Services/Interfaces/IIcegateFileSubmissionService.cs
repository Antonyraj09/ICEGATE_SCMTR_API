using Icegate.Integration.Models.Inbound;
using Microsoft.AspNetCore.Http;

namespace Icegate.Integration.Services.Interfaces;

public interface IIcegateFileSubmissionService
{
    Task<InboundUploadResultData> SubmitAsync(
        IFormFile file,
        string? localReferenceNo,
        string? icegateId,
        string? senderId,
        string correlationId,
        CancellationToken cancellationToken = default);
}

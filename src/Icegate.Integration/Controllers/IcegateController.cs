using Icegate.Integration.Middleware;
using Icegate.Integration.Models.Acknowledgement;
using Icegate.Integration.Models.Common;
using Icegate.Integration.Models.Inbound;
using Icegate.Integration.Models.ZipAcknowledgement;
using Icegate.Integration.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Icegate.Integration.Controllers;

/// <summary>
/// Internal API consumed by the legacy .NET Framework 4.0 application. Encapsulates all
/// ICEGATE SCMTR authentication/token/multipart complexity so the legacy application never
/// has to implement it directly. Secured separately via the internal API key middleware
/// (see <see cref="ApiKeyAuthMiddleware"/>) - independent of ICEGATE's own authentication.
/// </summary>
[ApiController]
[Route("api/icegate")]
[Produces("application/json")]
public class IcegateController : ControllerBase
{
    private readonly IIcegateFileSubmissionService _fileSubmissionService;
    private readonly IIcegateAcknowledgementService _acknowledgementService;
    private readonly IIcegateZipAcknowledgementService _zipAcknowledgementService;

    public IcegateController(
        IIcegateFileSubmissionService fileSubmissionService,
        IIcegateAcknowledgementService acknowledgementService,
        IIcegateZipAcknowledgementService zipAcknowledgementService)
    {
        _fileSubmissionService = fileSubmissionService;
        _acknowledgementService = acknowledgementService;
        _zipAcknowledgementService = zipAcknowledgementService;
    }

    /// <summary>
    /// Accepts an SCMTR JSON file from the legacy application, validates it, obtains/reuses
    /// an ICEGATE token, and submits it to the ICEGATE inbound upload API. Returns the
    /// ICEGATE-issued uniqueId on success.
    /// </summary>
    [HttpPost("inbound/upload")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(IcegateApiResult<InboundUploadResultData>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(IcegateApiResult<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(IcegateApiResult<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UploadInbound([FromForm] InboundUploadRequest request, CancellationToken cancellationToken)
    {
        var correlationId = HttpContext.GetCorrelationId();

        var data = await _fileSubmissionService.SubmitAsync(
            request.File, request.LocalReferenceNo, request.IcegateId, request.SenderId, correlationId, cancellationToken);

        return Ok(IcegateApiResult<InboundUploadResultData>.Ok(data, "File submitted successfully.", correlationId));
    }

    /// <summary>
    /// Retrieves the ICEGATE acknowledgement for a previously submitted SCMTR file, parses the
    /// multipart response, saves the ACK file, and returns its metadata. Returns HTTP 202 (not
    /// success) when the ACK has not yet been generated - callers should retry later.
    /// </summary>
    [HttpPost("ack")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(IcegateApiResult<AckResultData>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(IcegateApiResult<object>), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(IcegateApiResult<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAcknowledgement([FromBody] GetAckRequest request, CancellationToken cancellationToken)
    {
        var correlationId = HttpContext.GetCorrelationId();

        var data = await _acknowledgementService.GetAcknowledgementAsync(
            request.SenderId, request.UniqueId, correlationId, cancellationToken);

        return Ok(IcegateApiResult<AckResultData>.Ok(data, "Acknowledgement retrieved successfully.", correlationId));
    }

    /// <summary>
    /// Retrieves the batched ZIP acknowledgement from ICEGATE, parses the multipart response,
    /// saves the ZIP file, and returns a secure file reference (never the raw ZIP as Base64).
    /// </summary>
    [HttpPost("outbound/zip-ack")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(IcegateApiResult<ZipAckResultData>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(IcegateApiResult<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetZipAcknowledgement([FromBody] GetZipAckRequest request, CancellationToken cancellationToken)
    {
        var correlationId = HttpContext.GetCorrelationId();

        var data = await _zipAcknowledgementService.GetZipAcknowledgementAsync(request, correlationId, cancellationToken);

        return Ok(IcegateApiResult<ZipAckResultData>.Ok(data, "ZIP acknowledgement retrieved successfully.", correlationId));
    }
}

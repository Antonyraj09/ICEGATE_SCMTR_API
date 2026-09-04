using Icegate.Integration.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Icegate.Integration.Controllers;

/// <summary>
/// Liveness/readiness endpoints. Deliberately does NOT call ICEGATE or submit any file -
/// it only reports that this service is running and which ICEGATE endpoints are configured.
/// </summary>
[ApiController]
public class HealthController : ControllerBase
{
    private readonly IOptionsMonitor<IcegateSettings> _settings;

    public HealthController(IOptionsMonitor<IcegateSettings> settings)
    {
        _settings = settings;
    }

    [HttpGet("/health")]
    public IActionResult Health() => Ok(new
    {
        status = "Healthy",
        service = "Icegate.Integration",
        utcTime = DateTime.UtcNow
    });

    [HttpGet("/api/icegate/health")]
    public IActionResult IcegateHealth()
    {
        var settings = _settings.CurrentValue;

        return Ok(new
        {
            status = "Healthy",
            icegateEnvironment = settings.Environment,
            authenticationUrlConfigured = !string.IsNullOrWhiteSpace(settings.AuthenticationUrl),
            inboundUploadUrlConfigured = !string.IsNullOrWhiteSpace(settings.InboundUploadUrl),
            getAcknowledgementUrlConfirmed = settings.IsConfirmed(settings.GetAcknowledgementUrl),
            getZipAcknowledgementUrlConfigured = !string.IsNullOrWhiteSpace(settings.GetZipAcknowledgementUrl),
            utcTime = DateTime.UtcNow
        });
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Web.Common.Authorization;
using UmbracoCommunity.Web.Features.Seed;

namespace UmbracoCommunity.Web.Controllers.Api;

[ApiController]
[Route("seed")]
public sealed class SeedController : ControllerBase
{
    private readonly ISeedExportService _service;
    private readonly ILogger<SeedController> _logger;

    public SeedController(ISeedExportService service, ILogger<SeedController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>Downloads the latest snapshot zip. Public — referenced by contributor build script.</summary>
    [HttpGet("latest.zip")]
    public IActionResult Latest()
    {
        var path = _service.GetLatestZipPath();
        if (!System.IO.File.Exists(path))
        {
            return NotFound("No snapshot has been generated yet.");
        }

        return PhysicalFile(path, "application/zip", "import-on-startup.zip", enableRangeProcessing: true);
    }

    /// <summary>Returns last success/failure timestamps and in-progress state.</summary>
    [HttpGet("status")]
    public ActionResult<SeedExportStatus> Status() => _service.GetStatus();

    /// <summary>
    /// Triggers a regeneration in the background and returns 202 immediately.
    /// Returns 409 if a regeneration is already in flight.
    /// Requires Settings-section backoffice access.
    /// </summary>
    [HttpPost("regenerate")]
    [Authorize(Policy = AuthorizationPolicies.SectionAccessSettings)]
    public IActionResult Regenerate()
    {
        if (_service.GetStatus().IsRunning)
        {
            return Conflict(_service.GetStatus());
        }

        // Fire and forget. Status fields on the service capture progress/result.
        _ = Task.Run(async () =>
        {
            try
            {
                await _service.RegenerateAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Manual seed regenerate failed.");
            }
        });

        return Accepted(_service.GetStatus());
    }
}

using Microsoft.AspNetCore.Mvc;
using UmbracoCommunity.Web.Features.Seed;

namespace UmbracoCommunity.Web.Controllers.Api;

/// <summary>
/// Public endpoints for serving the latest seed export and surfacing its status.
/// The authenticated "regenerate" endpoint lives in the Extensions project as a backoffice
/// management API so it benefits from existing JWT auth plumbing.
/// </summary>
[ApiController]
[Route("seed")]
public sealed class SeedController : ControllerBase
{
    private readonly ISeedExportService _service;

    public SeedController(ISeedExportService service)
    {
        _service = service;
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
}

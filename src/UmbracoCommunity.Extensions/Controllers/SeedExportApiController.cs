using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Web.Common.Authorization;
using UmbracoCommunity.Web.Features.Seed;

namespace UmbracoCommunity.Extensions.Controllers;

[ApiVersion("1.0")]
[ApiExplorerSettings(GroupName = "UmbracoCommunity.Extensions")]
[Authorize(Policy = AuthorizationPolicies.SectionAccessSettings)]
public sealed class SeedExportApiController : UmbracoCommunityExtensionsApiControllerBase
{
    private readonly ISeedExportService _service;
    private readonly ILogger<SeedExportApiController> _logger;

    public SeedExportApiController(ISeedExportService service, ILogger<SeedExportApiController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet("seed/status")]
    [ProducesResponseType<SeedExportStatus>(StatusCodes.Status200OK)]
    public ActionResult<SeedExportStatus> Status() => _service.GetStatus();

    [HttpPost("seed/regenerate")]
    [ProducesResponseType<SeedExportStatus>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<SeedExportStatus>(StatusCodes.Status409Conflict)]
    public IActionResult Regenerate()
    {
        if (_service.GetStatus().IsRunning)
        {
            return Conflict(_service.GetStatus());
        }

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

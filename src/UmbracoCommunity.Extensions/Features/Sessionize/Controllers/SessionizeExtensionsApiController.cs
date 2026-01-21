using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using UmbracoCommunity.Extensions.Features.Sessionize.Models;
using UmbracoCommunity.Extensions.Infrastructure;
using UmbracoCommunity.Web.Features.Sessionize.Infrastructure;

namespace UmbracoCommunity.Extensions.Features.Sessionize.Controllers;

[ApiVersion("1.0")]
[ApiExplorerSettings(GroupName = "UmbracoCommunity.Extensions")]
public class SessionizeExtensionsApiController : UmbracoCommunityExtensionsApiControllerBase
{
    private readonly SessionizeApiClient _sessionizeApiClient;

    public SessionizeExtensionsApiController(SessionizeApiClient sessionizeApiClient)
    {
        _sessionizeApiClient = sessionizeApiClient;
    }

    /// <summary>
    /// Clears the Sessionize cache to force fresh data on next request
    /// </summary>
    [HttpPost("sessionize/refresh-cache", Name = "RefreshSessionizeCache")]
    [ProducesResponseType<SessionizeCacheRefreshResult>(StatusCodes.Status200OK)]
    public IActionResult RefreshSessionizeCache()
    {
        _sessionizeApiClient.ClearCache();
        return Ok(new SessionizeCacheRefreshResult
        {
            Success = true,
            Message = "Sessionize cache cleared successfully. Fresh data will be fetched on next request.",
            RefreshedAt = DateTime.UtcNow
        });
    }
}

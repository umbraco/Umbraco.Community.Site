using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using UmbracoCommunity.Web.Features.Sessionize.Infrastructure;

namespace UmbracoCommunity.Extensions.Controllers
{
    [ApiVersion("1.0")]
    [ApiExplorerSettings(GroupName = "UmbracoCommunity.Extensions")]
    public class UmbracoCommunityExtensionsApiController : UmbracoCommunityExtensionsApiControllerBase
    {
        private readonly SessionizeApiClient _sessionizeApiClient;

        public UmbracoCommunityExtensionsApiController(
            SessionizeApiClient sessionizeApiClient)
        {
            _sessionizeApiClient = sessionizeApiClient;
        }

        #region Sessionize

        [HttpPost("clear-sessionize-cache")]
        [ProducesResponseType<string>(StatusCodes.Status200OK)]
        public IActionResult ClearSessionizeCache()
        {
            _sessionizeApiClient.ClearCache();
            return Ok("Sessionize cache cleared successfully");
        }

        #endregion
    }
}

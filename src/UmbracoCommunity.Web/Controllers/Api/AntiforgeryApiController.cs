using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Security;
using UmbracoCommunity.Web.Models.Api;

namespace UmbracoCommunity.Web.Controllers.Api;

/// <summary>
/// Hands a signed-in member the antiforgery request token that the member APIs
/// (<c>api/profile</c>, <c>api/member-feeds</c>) require on every POST, PUT and DELETE. The
/// token is bound to the member's identity and paired with the antiforgery cookie this call
/// sets, so it is fetched on demand rather than rendered into (possibly cached) pages.
/// </summary>
[ApiController]
[Route("api/antiforgery")]
public class AntiforgeryApiController : ControllerBase
{
    private readonly IMemberManager _memberManager;
    private readonly IAntiforgery _antiforgery;

    public AntiforgeryApiController(IMemberManager memberManager, IAntiforgery antiforgery)
    {
        _memberManager = memberManager;
        _antiforgery = antiforgery;
    }

    [HttpGet("token")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult Token()
    {
        if (!_memberManager.IsLoggedIn())
        {
            return Unauthorized();
        }

        var tokens = _antiforgery.GetAndStoreTokens(HttpContext);
        if (tokens.RequestToken == null || tokens.HeaderName == null)
        {
            return Problem("Could not create an antiforgery token.");
        }

        return Ok(new AntiforgeryTokenResponse(tokens.RequestToken, tokens.HeaderName));
    }
}

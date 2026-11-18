using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Umbraco.Cms.Core.Security;
using UmbracoCommunity.Web.Extensions;
using UmbracoCommunity.Web.Features.OpenBadges.Infrastructure;

namespace UmbracoCommunity.Web.Features.OpenBadges.Controllers;

/// <summary>
/// The Open Badges 2.0 endpoints for the "Umbraco MVP" badges, routed in <c>UseApplicationEndpoints</c>
/// (see <see cref="OpenBadgesRoutes"/>). The JSON-LD documents and the badge image are public, so verifiers can fetch
/// them; the baked download is only for the signed-in member, and only for their current MVP year.
/// Every endpoint returns 404 while <c>OpenBadges:SigningKey</c> is not configured.
/// </summary>
public class OpenBadgesController(MvpBadgeService badges, IMemberManager memberManager) : Controller
{
    private const string JsonLdContentType = "application/ld+json";
    private const string JsonContentType = "application/json";
    private const string PngContentType = "image/png";

    [HttpGet]
    [OutputCache(PolicyName = OutputCachePolicies.ExternalApi, VaryByHeaderNames = ["Accept"])]
    public IActionResult Issuer() => Document(badges.GetIssuer(BaseUri));

    [HttpGet]
    [OutputCache(PolicyName = OutputCachePolicies.ExternalApi, VaryByHeaderNames = ["Accept"])]
    public IActionResult MvpBadgeClass(int year) => Document(badges.GetBadgeClass(BaseUri, year));

    [HttpGet]
    [OutputCache(PolicyName = OutputCachePolicies.ExternalApi)]
    public IActionResult MvpImage(int year)
    {
        var image = badges.GetImage(year);
        if (image is null)
        {
            return NotFound();
        }

        AllowAnyOrigin();
        return File(image, PngContentType);
    }

    [HttpGet]
    [OutputCache(PolicyName = OutputCachePolicies.ExternalApi, VaryByHeaderNames = ["Accept"])]
    public async Task<IActionResult> Assertion(string id, CancellationToken cancellationToken) =>
        Document(await badges.GetAssertionAsync(BaseUri, id, cancellationToken));

    /// <summary>
    /// The signed-in member's badge image with their assertion baked in. A GET, so it works as a plain download link:
    /// it changes nothing, and another site can't read the response, so it needs no antiforgery token.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> MvpDownload(int year, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";

        var member = await memberManager.GetCurrentMemberAsync();
        if (member?.UserName is null)
        {
            return Unauthorized();
        }

        var baked = await badges.BakeAsync(BaseUri, member.UserName, year, cancellationToken);
        return baked is null
            ? NotFound()
            : File(baked, PngContentType, OpenBadgesRoutes.MvpDownloadFileName(year));
    }

    private Uri BaseUri => new($"{Request.Scheme}://{Request.Host}/");

    /// <summary>
    /// JSON-LD by default, as Open Badges 2.0 asks; plain <c>application/json</c> for a client that only accepts that.
    /// </summary>
    private IActionResult Document(JsonObject? document)
    {
        if (document is null)
        {
            return NotFound();
        }

        AllowAnyOrigin();
        return Content(document.ToJsonString(), PreferredJsonContentType(Request.Headers.Accept.ToString()));
    }

    internal static string PreferredJsonContentType(string? accept)
    {
        if (string.IsNullOrWhiteSpace(accept)
            || accept.Contains(JsonLdContentType, StringComparison.OrdinalIgnoreCase)
            || !accept.Contains(JsonContentType, StringComparison.OrdinalIgnoreCase))
        {
            return JsonLdContentType;
        }

        return JsonContentType;
    }

    // Public badge data: browser-based verifiers and backpacks fetch it from other origins.
    private void AllowAnyOrigin() => Response.Headers.AccessControlAllowOrigin = "*";
}

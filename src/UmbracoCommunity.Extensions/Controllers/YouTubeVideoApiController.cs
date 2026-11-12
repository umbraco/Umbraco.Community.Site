using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Security;
using UmbracoCommunity.Extensions.Infrastructure;
using UmbracoCommunity.Extensions.Models;
using UmbracoCommunity.Web.Features.YouTube;

namespace UmbracoCommunity.Extensions.Controllers;

/// <summary>
/// Backs the YouTube video picker: looks up a video's title and stills, and imports the chosen still into the
/// Media library as its poster.
/// </summary>
[ApiVersion("1.0")]
[ApiExplorerSettings(GroupName = "UmbracoCommunity.Extensions")]
public sealed class YouTubeVideoApiController : UmbracoCommunityExtensionsApiControllerBase
{
    private readonly IYouTubePosterService _posters;
    private readonly IBackOfficeSecurityAccessor _backOfficeSecurityAccessor;

    public YouTubeVideoApiController(IYouTubePosterService posters, IBackOfficeSecurityAccessor backOfficeSecurityAccessor)
    {
        _posters = posters;
        _backOfficeSecurityAccessor = backOfficeSecurityAccessor;
    }

    [HttpGet("youtube/preview")]
    [ProducesResponseType<YouTubeVideoPreview>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<YouTubeVideoPreview>> GetPreview([FromQuery] string url, CancellationToken cancellationToken)
    {
        var preview = await _posters.GetPreviewAsync(url, cancellationToken);
        return preview is null ? NotAYouTubeVideo() : preview;
    }

    [HttpPost("youtube/posters")]
    [ProducesResponseType<YouTubePoster>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<YouTubePoster>> ImportPoster(ImportYouTubePosterRequest request, CancellationToken cancellationToken)
    {
        if (_backOfficeSecurityAccessor.BackOfficeSecurity?.CurrentUser is not { } user) return Forbid();

        var poster = await _posters.ImportStillAsync(request.Url, request.Variant, request.ContentKey, request.Title, user.Id, cancellationToken);
        return poster is null
            ? BadRequest(new ProblemDetails { Title = "Couldn't import the still", Detail = "Check the link is a public YouTube video, then try another still." })
            : poster;
    }

    [HttpGet("youtube/posters/{mediaKey:guid}")]
    [ProducesResponseType<YouTubePoster>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<YouTubePoster> GetPoster(Guid mediaKey)
        => _posters.GetPoster(mediaKey) is { } poster ? poster : NotFound();

    private BadRequestObjectResult NotAYouTubeVideo()
        => BadRequest(new ProblemDetails { Title = "Not a YouTube video", Detail = "Paste a link to a single YouTube video, such as https://www.youtube.com/watch?v=…" });
}

using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Security;
using UmbracoCommunity.Web.Features.Feeds.CommunityBlogs;
using UmbracoCommunity.Web.Features.Profiles.Data.Entities;
using UmbracoCommunity.Web.Features.Profiles.LinkPreviews;
using UmbracoCommunity.Web.Features.Profiles.Matching;
using UmbracoCommunity.Web.Features.Profiles.Models;
using UmbracoCommunity.Web.Features.Profiles.Data;

namespace UmbracoCommunity.Web.Features.Profiles.Controllers;

/// <summary>
/// Feed management API — always scoped to the current signed-in member, never trusting an
/// id-in-URL for whose feeds are being modified. Shared between the onboarding wizard's
/// feeds step and the Account page (same endpoints, same behaviour in both places).
/// Every POST, PUT and DELETE must carry the antiforgery request token from
/// <c>api/antiforgery/token</c> in the <c>RequestVerificationToken</c> header.
/// For members whose links are checked (see <see cref="ProfileAccessService.CanMatchLinksAsync"/>) a new or changed
/// address is checked after it is saved, and each link is listed with its match status.
/// </summary>
[ApiController]
[AutoValidateAntiforgeryToken]
[Route("api/member-feeds")]
public class MemberFeedsApiController : ControllerBase
{
    private readonly IMemberManager _memberManager;
    private readonly MemberProfileStore _store;
    private readonly IProfileSyncClient _profileSyncClient;
    private readonly LinkPreviewService _linkPreviews;
    private readonly FeedSubmissionImageProxyService _imageProxy;
    private readonly ProfileAccessService _profileAccessService;
    private readonly ProfileLinkMatchingService _linkMatching;

    public MemberFeedsApiController(
        IMemberManager memberManager,
        MemberProfileStore store,
        IProfileSyncClient profileSyncClient,
        LinkPreviewService linkPreviews,
        FeedSubmissionImageProxyService imageProxy,
        ProfileAccessService profileAccessService,
        ProfileLinkMatchingService linkMatching)
    {
        _memberManager = memberManager;
        _store = store;
        _profileSyncClient = profileSyncClient;
        _linkPreviews = linkPreviews;
        _imageProxy = imageProxy;
        _profileAccessService = profileAccessService;
        _linkMatching = linkMatching;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var member = await _memberManager.GetCurrentMemberAsync();
        if (member == null)
        {
            return Unauthorized();
        }

        var feeds = await _store.GetActiveFeedsAsync(member.Key, cancellationToken);
        IReadOnlyDictionary<string, string> matchStatuses = new Dictionary<string, string>();
        if (feeds.Count > 0 && await _profileAccessService.CanMatchLinksAsync())
        {
            matchStatuses = await _linkMatching.GetLinkStatusesAsync(
                feeds[0].MemberProfileId, feeds.Select(f => f.Url).ToList(), cancellationToken);
        }

        return Ok(feeds.Select(f => ToResponse(f, matchStatuses.GetValueOrDefault(f.Url))));
    }

    [HttpPost]
    public async Task<IActionResult> Add([FromBody] AddFeedRequest request, CancellationToken cancellationToken)
    {
        var member = await _memberManager.GetCurrentMemberAsync();
        if (member == null)
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.Platform))
        {
            return BadRequest(new { error = "Please provide a platform name." });
        }

        if (!ProfileLinkUrl.TryParse(request.Url, out _))
        {
            return BadRequest(new { error = "Please provide a valid https URL." });
        }

        var preview = await _linkPreviews.FetchAsync(request.Url.Trim(), cancellationToken);
        var feed = await _store.AddFeedAsync(member.Key, request.Platform.Trim(), request.Url.Trim(), preview, cancellationToken);
        if (feed == null)
        {
            return BadRequest(new { error = "Start onboarding before adding feeds." });
        }

        await _profileSyncClient.NotifyFeedAddedAsync(member.UserName ?? string.Empty, feed.Platform, feed.Url, cancellationToken);

        return Ok(await WithMatchStatusAsync(feed, member.UserName, checkFirst: true, cancellationToken));
    }

    /// <summary>Changes a link's platform name and address. A new address is re-checked and reported to the platform as a swap.</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateFeedRequest request, CancellationToken cancellationToken)
    {
        var member = await _memberManager.GetCurrentMemberAsync();
        if (member == null)
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.Platform))
        {
            return BadRequest(new { error = "Please provide a platform name." });
        }

        if (!ProfileLinkUrl.TryParse(request.Url, out _))
        {
            return BadRequest(new { error = "Please provide a valid https URL." });
        }

        var existing = (await _store.GetActiveFeedsAsync(member.Key, cancellationToken)).FirstOrDefault(f => f.Id == id);
        if (existing == null)
        {
            return NotFound();
        }

        var url = request.Url.Trim();
        var oldUrl = existing.Url;
        var urlChanged = !string.Equals(oldUrl, url, StringComparison.Ordinal);
        var preview = urlChanged ? await _linkPreviews.FetchAsync(url, cancellationToken) : null;

        var feed = await _store.UpdateFeedAsync(member.Key, id, request.Platform.Trim(), url, preview, cancellationToken);
        if (feed == null)
        {
            return NotFound();
        }

        if (urlChanged)
        {
            var handle = member.UserName ?? string.Empty;
            await _profileSyncClient.NotifyFeedRemovedAsync(handle, oldUrl, "Address changed", cancellationToken);
            await _profileSyncClient.NotifyFeedAddedAsync(handle, feed.Platform, feed.Url, cancellationToken);
        }

        return Ok(await WithMatchStatusAsync(feed, member.UserName, checkFirst: urlChanged, cancellationToken));
    }

    /// <summary>Stores the order of all of the member's active links. The ids must be exactly those links.</summary>
    [HttpPut("order")]
    public async Task<IActionResult> Reorder([FromBody] ReorderFeedsRequest request, CancellationToken cancellationToken)
    {
        var member = await _memberManager.GetCurrentMemberAsync();
        if (member == null)
        {
            return Unauthorized();
        }

        if (!await _store.ReorderFeedsAsync(member.Key, request.Ids, cancellationToken))
        {
            return BadRequest(new { error = "The list of links is out of date. Reload and try again." });
        }

        return Ok();
    }

    [HttpPut("{id:int}/hide")]
    public Task<IActionResult> Hide(int id, CancellationToken cancellationToken) => SetHidden(id, true, cancellationToken);

    [HttpPut("{id:int}/unhide")]
    public Task<IActionResult> Unhide(int id, CancellationToken cancellationToken) => SetHidden(id, false, cancellationToken);

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Remove(int id, [FromBody] RemoveFeedRequest? request, CancellationToken cancellationToken)
    {
        var member = await _memberManager.GetCurrentMemberAsync();
        if (member == null)
        {
            return Unauthorized();
        }

        var feeds = await _store.GetActiveFeedsAsync(member.Key, cancellationToken);
        var feed = feeds.FirstOrDefault(f => f.Id == id);
        if (feed == null)
        {
            return NotFound();
        }

        var removed = await _store.RemoveFeedAsync(member.Key, id, request?.Reason, cancellationToken);
        if (!removed)
        {
            return NotFound();
        }

        await _profileSyncClient.NotifyFeedRemovedAsync(member.UserName ?? string.Empty, feed.Url, request?.Reason, cancellationToken);

        return Ok();
    }

    private async Task<IActionResult> SetHidden(int id, bool isHidden, CancellationToken cancellationToken)
    {
        var member = await _memberManager.GetCurrentMemberAsync();
        if (member == null)
        {
            return Unauthorized();
        }

        var feeds = await _store.GetActiveFeedsAsync(member.Key, cancellationToken);
        var feed = feeds.FirstOrDefault(f => f.Id == id);
        if (feed == null)
        {
            return NotFound();
        }

        var updated = await _store.SetFeedHiddenAsync(member.Key, id, isHidden, cancellationToken);
        if (!updated)
        {
            return NotFound();
        }

        await _profileSyncClient.NotifyFeedHiddenAsync(member.UserName ?? string.Empty, feed.Url, isHidden, cancellationToken);

        return Ok();
    }

    /// <summary>
    /// For a member whose links are checked, returns a saved link with its match status, checking the link first when
    /// <paramref name="checkFirst"/> is set. For everyone else the link is returned as it is.
    /// </summary>
    private async Task<MemberFeedResponse> WithMatchStatusAsync(
        MemberFeedEntity feed, string? handle, bool checkFirst, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(handle) || !await _profileAccessService.CanMatchLinksAsync())
        {
            return ToResponse(feed);
        }

        if (checkFirst)
        {
            await _linkMatching.CheckLinksAsync(feed.MemberProfileId, handle, [feed.Url], cancellationToken);
        }

        var statuses = await _linkMatching.GetLinkStatusesAsync(feed.MemberProfileId, [feed.Url], cancellationToken);
        return ToResponse(feed, statuses.GetValueOrDefault(feed.Url));
    }

    /// <summary>
    /// What the member sees next to a link: "Hidden" (kept off the public profile), "Unreachable" (the last
    /// server-side check couldn't load the page) or "Visible".
    /// </summary>
    private static string StatusOf(MemberFeedEntity feed) =>
        feed.IsHidden ? "Hidden"
        : feed.PreviewStatus == LinkPreviewStatus.Unreachable ? "Unreachable"
        : "Visible";

    private MemberFeedResponse ToResponse(MemberFeedEntity feed) => ToResponse(feed, null);

    private MemberFeedResponse ToResponse(MemberFeedEntity feed, string? matchStatus) => new(
        feed.Id,
        feed.Platform,
        feed.Url,
        feed.IsHidden,
        feed.Source.ToString(),
        StatusOf(feed),
        feed.PreviewTitle,
        _imageProxy.CreateProxyUrl(feed.FaviconUrl),
        _imageProxy.CreateProxyUrl(feed.PreviewImageUrl),
        matchStatus);
}

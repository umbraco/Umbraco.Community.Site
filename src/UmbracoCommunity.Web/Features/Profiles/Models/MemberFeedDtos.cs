namespace UmbracoCommunity.Web.Features.Profiles.Models;

/// <param name="Status">"Visible", "Hidden" or "Unreachable" — see <c>MemberFeedsApiController</c>.</param>
/// <param name="FaviconUrl">Same-origin proxy URL, or null.</param>
/// <param name="PreviewImageUrl">Same-origin proxy URL, or null.</param>
/// <param name="MatchStatus">
/// Whether the link was checked as the member's: "Confirmed", "Held" (waiting for a review) or "Rejected". Null when
/// it has no decision, or the member's links are not checked (see <c>ProfileAccessService.CanMatchLinksAsync</c>).
/// </param>
public sealed record MemberFeedResponse(
    int Id,
    string Platform,
    string Url,
    bool IsHidden,
    string Source,
    string Status,
    string? PreviewTitle,
    string? FaviconUrl,
    string? PreviewImageUrl,
    string? MatchStatus = null);

public sealed record AddFeedRequest(string Platform, string Url);

public sealed record UpdateFeedRequest(string Platform, string Url);

public sealed record ReorderFeedsRequest(List<int> Ids);

public sealed record RemoveFeedRequest(string? Reason);

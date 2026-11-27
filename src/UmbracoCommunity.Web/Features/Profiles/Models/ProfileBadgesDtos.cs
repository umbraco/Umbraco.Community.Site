namespace UmbracoCommunity.Web.Features.Profiles.Models;

/// <summary>The member's forum badges for the account page's Badges panel (see <c>ProfileBadgesApiController</c>).</summary>
/// <param name="Status">
/// "ok" (the badges are listed, possibly none), "noForumLink" (the member has no link to their forum profile),
/// "notFound" (the forum has no user by the name in that link) or "unavailable" (the forum could not be reached; try
/// again later).
/// </param>
/// <param name="ForumUsername">The forum username from the member's forum link, or null without one.</param>
/// <param name="ForumProfileUrl">That user's page on the forum, or null.</param>
/// <param name="Badges">Every badge the member holds on the forum, in the forum's order.</param>
/// <param name="ShownIds">The ids of the badges shown on the profile, in <paramref name="Badges"/> order.</param>
/// <param name="MaxShown">How many badges can be shown at most.</param>
public sealed record ProfileBadgesResponse(
    string Status,
    string? ForumUsername,
    string? ForumProfileUrl,
    IReadOnlyList<ProfileBadgeResponse> Badges,
    IReadOnlyList<int> ShownIds,
    int MaxShown);

/// <param name="IconUrl">The badge's picture through this site's image proxy, or null to show the generic badge icon.</param>
/// <param name="BadgeType">"Gold", "Silver" or "Bronze", or null.</param>
/// <param name="GrantCount">How many forum members have the badge, or null.</param>
/// <param name="Url">The badge's page on the forum, or null.</param>
public sealed record ProfileBadgeResponse(
    int Id,
    string Name,
    string? Description,
    string? IconUrl,
    string? BadgeType,
    int? GrantCount,
    DateTimeOffset? GrantedAt,
    bool Shown,
    string? Url);

public sealed record UpdateProfileBadgesRequest(List<int>? BadgeIds);

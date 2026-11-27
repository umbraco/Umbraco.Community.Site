using UmbracoCommunity.Web.Features.Feeds.CommunityBlogs;
using UmbracoCommunity.Web.Features.Profiles.Data.Entities;
using UmbracoCommunity.Web.Features.Profiles.Models;

namespace UmbracoCommunity.Web.Features.Profiles.ForumBadges;

/// <summary>
/// A member's Umbraco forum badges and the ones they chose to show on their profile. The forum account is the one their
/// own link to their forum profile points at (<see cref="ForumProfileUrl.FindUsername"/>); the forum username can differ
/// from the GitHub handle, so there is no fallback to it (a same-named stranger's badges must never show). Without such
/// a link there are no badges. A chosen badge the member doesn't hold (for example after changing the link) is skipped,
/// and if the forum has no such user or can't be reached nothing is shown rather than failing the page. This is the one
/// place the username is worked out, for the API, the public profile and the account page header alike.
/// </summary>
public sealed class ForumBadgeService
{
    /// <summary>The most badges a member can show on their profile.</summary>
    public const int MaxShown = 8;

    private readonly ForumBadgeClient _client;
    private readonly FeedSubmissionImageProxyService _imageProxy;

    public ForumBadgeService(ForumBadgeClient client, FeedSubmissionImageProxyService imageProxy)
    {
        _client = client;
        _imageProxy = imageProxy;
    }

    /// <summary>
    /// The forum username from the member's forum link and that forum user's badges. The username is null (and the forum
    /// is not asked) when the member has no forum link; <paramref name="profile"/> must have its feeds loaded.
    /// </summary>
    public async Task<(string? Username, ForumBadgesResult Result)> GetMemberBadgesAsync(
        MemberProfileEntity profile, CancellationToken cancellationToken = default)
    {
        var username = ForumProfileUrl.FindUsername(profile.Feeds);
        return username == null
            ? (null, ForumBadgesResult.NotFound)
            : (username, await _client.GetBadgesAsync(username, cancellationToken));
    }

    /// <summary>The badges the member chose to show, as their profile shows them. Asks the forum only when they chose some.</summary>
    public async Task<IReadOnlyList<ProfileBadge>> GetShownBadgesAsync(MemberProfileEntity profile, CancellationToken cancellationToken = default)
    {
        if (profile.ShownForumBadgeIds.Count == 0)
        {
            return [];
        }

        var (_, result) = await GetMemberBadgesAsync(profile, cancellationToken);
        return Shown(result.Badges, profile.ShownForumBadgeIds).Select(ToProfileBadge).ToList();
    }

    /// <summary>The chosen badges the member holds, in the forum's order, at most <see cref="MaxShown"/>.</summary>
    public static IReadOnlyList<ForumBadge> Shown(IReadOnlyList<ForumBadge> owned, IReadOnlyCollection<int> chosenIds)
    {
        if (chosenIds.Count == 0)
        {
            return [];
        }

        var chosen = chosenIds.ToHashSet();
        return owned.Where(b => chosen.Contains(b.Id)).Take(MaxShown).ToList();
    }

    /// <summary>
    /// Checks a member's new choice of badges against the badges they hold: duplicates and badges they don't hold are
    /// dropped, and more than <see cref="MaxShown"/> is refused with a message for the member.
    /// </summary>
    public static bool TryNormalizeChoice(
        IEnumerable<int>? requestedIds, IReadOnlyList<ForumBadge> owned, out IReadOnlyList<int> ids, out string? error)
    {
        var ownedIds = owned.Select(b => b.Id).ToHashSet();
        var kept = (requestedIds ?? []).Distinct().Where(ownedIds.Contains).ToList();
        if (kept.Count > MaxShown)
        {
            ids = [];
            error = $"You can show up to {MaxShown} badges. Untick one to show another.";
            return false;
        }

        ids = kept;
        error = null;
        return true;
    }

    /// <summary>
    /// A badge as the profile shows it. Its picture is served through the image proxy, so the browser only ever loads it
    /// from this site; a badge with only an icon name gets the profile's own badge icon instead.
    /// </summary>
    public ProfileBadge ToProfileBadge(ForumBadge badge) =>
        new(badge.Name, _imageProxy.CreateProxyUrl(badge.ImageUrl), badge.Description, badge.GrantedAt, badge.BadgeType, badge.Url);
}

namespace UmbracoCommunity.Web.Features.Profiles.Models;

/// <summary>
/// A community member's public profile, aggregated from curated/external data.
/// Source-agnostic: the same shape is produced by the Phase 1 dummy provider and
/// the later platform-backed provider, so the view never changes between phases.
/// </summary>
/// <remarks>
/// <para><see cref="Slug"/> is the member's GitHub handle (the public URL token).
/// <see cref="PlatformId"/> is the external platform's internal GUID, kept for linking back and
/// the later claim flow.</para>
/// <para>All collections are non-null (empty when absent) so the view can render
/// "nothing here yet" states without null checks.</para>
/// <para>Items that came from the external content platform carry its activity id
/// (<c>ActivityId</c>), which is what a member's hidden and pinned items refer to.</para>
/// </remarks>
public sealed record CommunityProfile(
    string Slug,
    string? PlatformId,
    ProfileIdentity Identity,
    IReadOnlyList<ProfileBlogPost> BlogPosts,
    IReadOnlyList<ProfileVideo> Videos,
    IReadOnlyList<ProfileForumPost> ForumPosts,
    IReadOnlyList<ProfilePackage> Packages,
    IReadOnlyList<ProfileTalk> Talks,
    IReadOnlyList<ProfileBadge> Badges,
    IReadOnlyList<ProfileSocialPost> SocialPosts,
    IReadOnlyList<ProfileMeetup> Meetups)
{
    /// <summary>What the member has chosen the activity sections show. Applied by <see cref="ProfileDisplay.Apply"/>.</summary>
    public ProfileDisplayChoices DisplayChoices { get; init; } = ProfileDisplayChoices.None;

    /// <summary>Activity the member added by hand that has no section of its own (a talk, a contribution, a podcast, anything else).</summary>
    public IReadOnlyList<ProfileOtherActivity> OtherActivities { get; init; } = [];

    /// <summary>
    /// The member's pinned activities, in their order. Empty until <see cref="ProfileDisplay.Apply"/> has run,
    /// and when nothing pinned can be shown.
    /// </summary>
    public IReadOnlyList<ProfileFeaturedItem> FeaturedItems { get; init; } = [];
}

/// <summary>Core identity and "about" details shown in the profile header.</summary>
/// <remarks><see cref="AvatarUrl"/> is resolved by the provider from
/// <see cref="GitHubHandle"/> (<c>https://github.com/{handle}.png</c>), falling back
/// to a platform-supplied value, then to a default placeholder in the view.</remarks>
public sealed record ProfileIdentity(
    string DisplayName,
    string? Headline,
    string? Bio,
    string? AvatarUrl,
    string? Location,
    string? Pronouns,
    bool IsMvp,
    IReadOnlyList<int> MvpYears,
    string? GitHubHandle,
    string? YouTubeChannelUrl,
    IReadOnlyList<ProfileSocialLink> SocialLinks,
    bool HasCustomAvatar)
{
    /// <summary>True when the newest of <see cref="MvpYears"/> is the current MVP year. Set by <see cref="ProfileMvp"/>.</summary>
    public bool IsCurrentMvp { get; init; }
}

/// <summary>A blog post or article the member has written, from any source.</summary>
public sealed record ProfileBlogPost(
    string Title,
    string Url,
    DateTimeOffset PublishedAt,
    string? Source,
    string? Excerpt = null,
    string? ActivityId = null);

/// <summary>A YouTube video. May be sourced from the external platform or derived from the channel link.</summary>
/// <param name="ThumbnailUrl">
/// The thumbnail's address on its own site. Never put in front of a visitor as it is: the timeline shows a copy
/// stored by <see cref="ProfileTimelineImageStore"/>.
/// </param>
public sealed record ProfileVideo(
    string Title,
    string Url,
    string? ThumbnailUrl,
    DateTimeOffset? PublishedAt,
    string? Description = null,
    string? ActivityId = null);

/// <summary>A post or reply on the Umbraco community forum.</summary>
public sealed record ProfileForumPost(
    string Title,
    string Url,
    DateTimeOffset PostedAt,
    int? Replies,
    string? ActivityId = null);

/// <summary>A released package and its published versions (newest first).</summary>
/// <param name="Date">When the package was last active (its latest release, or the date the member gave). Null when not known.</param>
public sealed record ProfilePackage(
    string Name,
    string Url,
    IReadOnlyList<string> Versions,
    string? Description = null,
    string? ActivityId = null,
    DateTimeOffset? Date = null);

/// <summary>A talk given by the member, sourced from Sessionize.</summary>
public sealed record ProfileTalk(
    string Title,
    string? EventName,
    DateTimeOffset? Date,
    string? Url,
    string? Description = null);

/// <summary>A badge earned by the member (e.g. from the forum profile).</summary>
public sealed record ProfileBadge(
    string Name,
    string? IconUrl,
    string? Description,
    DateTimeOffset? AwardedAt);

/// <summary>A link to the member's presence on an external platform.</summary>
/// <param name="IsHiddenByMember">
/// True when the member hid the link. Such a link is only included on a DevRel view that shows everything
/// (see <see cref="ProfileAccessService.GetViewableProfileAsync"/>).
/// </param>
public sealed record ProfileSocialLink(
    string Platform,
    string Url,
    string? FaviconUrl = null,
    bool IsHiddenByMember = false);

/// <summary>A post the member made on a social platform (LinkedIn, Mastodon, Bluesky).</summary>
public sealed record ProfileSocialPost(
    string Platform,
    string Url,
    string Text,
    DateTimeOffset PostedAt,
    string? ActivityId = null);

/// <summary>
/// A meet-up activity. Reads as a sentence, e.g.
/// "{Action} {EventName}, {Group}, {date}" → "RSVP'd yes to UmbLeeds: Codegarden review,
/// Umbraco Leeds Meetup group, 18th June 2026".
/// </summary>
public sealed record ProfileMeetup(
    string Action,
    string EventName,
    string? Group,
    DateTimeOffset Date,
    string? Url = null,
    string? ActivityId = null);

/// <summary>A piece of activity the member added by hand; <paramref name="Kind"/> says what it is (e.g. "Talk").</summary>
public sealed record ProfileOtherActivity(
    string Kind,
    string Title,
    string Url,
    DateTimeOffset Date,
    string? Description = null,
    string? ActivityId = null);

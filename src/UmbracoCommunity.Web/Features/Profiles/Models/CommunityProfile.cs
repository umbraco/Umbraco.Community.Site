namespace UmbracoCommunity.Web.Features.Profiles.Models;

/// <summary>
/// A community member's public profile, aggregated from curated/external data.
/// Source-agnostic: the same shape is produced by the Phase 1 dummy provider and
/// the later Sphere-backed provider, so the view never changes between phases.
/// </summary>
/// <remarks>
/// <para><see cref="Slug"/> is the member's GitHub handle (the public URL token).
/// <see cref="SphereId"/> is Sphere's internal GUID, kept for linking back and the
/// later claim flow.</para>
/// <para>All collections are non-null (empty when absent) so the view can render
/// "nothing here yet" states without null checks.</para>
/// </remarks>
public sealed record CommunityProfile(
    string Slug,
    string? SphereId,
    ProfileIdentity Identity,
    IReadOnlyList<ProfileBlogPost> BlogPosts,
    IReadOnlyList<ProfileVideo> Videos,
    IReadOnlyList<ProfileForumPost> ForumPosts,
    IReadOnlyList<ProfilePackage> Packages,
    IReadOnlyList<ProfileTalk> Talks,
    IReadOnlyList<ProfileBadge> Badges,
    IReadOnlyList<ProfileSocialPost> SocialPosts,
    IReadOnlyList<ProfileMeetup> Meetups);

/// <summary>Core identity and "about" details shown in the profile header.</summary>
/// <remarks><see cref="AvatarUrl"/> is resolved by the provider from
/// <see cref="GitHubHandle"/> (<c>https://github.com/{handle}.png</c>), falling back
/// to a Sphere-supplied value, then to a default placeholder in the view.</remarks>
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
    bool HasCustomAvatar);

/// <summary>A blog post or article the member has written, from any source.</summary>
public sealed record ProfileBlogPost(
    string Title,
    string Url,
    DateTimeOffset PublishedAt,
    string? Source,
    string? Excerpt = null);

/// <summary>A YouTube video. May be sourced from Sphere or derived from the channel link.</summary>
public sealed record ProfileVideo(
    string Title,
    string Url,
    string? ThumbnailUrl,
    DateTimeOffset? PublishedAt,
    string? Description = null);

/// <summary>A post or reply on the Umbraco community forum.</summary>
public sealed record ProfileForumPost(
    string Title,
    string Url,
    DateTimeOffset PostedAt,
    int? Replies);

/// <summary>A released package and its published versions (newest first).</summary>
public sealed record ProfilePackage(
    string Name,
    string Url,
    IReadOnlyList<string> Versions,
    string? Description = null);

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
public sealed record ProfileSocialLink(
    string Platform,
    string Url);

/// <summary>A post the member made on a social platform (LinkedIn, Mastodon, Bluesky).</summary>
public sealed record ProfileSocialPost(
    string Platform,
    string Url,
    string Text,
    DateTimeOffset PostedAt);

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
    string? Url = null);

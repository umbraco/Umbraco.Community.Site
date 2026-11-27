using UmbracoCommunity.Web.Features.Feeds.CommunityBlogs;
using UmbracoCommunity.Web.Features.Profiles.Data;
using UmbracoCommunity.Web.Features.Profiles.Data.Entities;
using UmbracoCommunity.Web.Features.Profiles.ForumBadges;
using UmbracoCommunity.Web.Features.Profiles.Models;

namespace UmbracoCommunity.Web.Features.Profiles;

/// <summary>
/// Live profile source backed by <see cref="MemberProfileStore"/>. Replaces
/// <see cref="DummyProfileDataProvider"/> in DI. Only returns a profile for members who
/// have completed onboarding (see <see cref="OnboardingStatus.Completed"/>); an in-progress
/// onboarding is never visible. Profiles start hidden (see
/// <see cref="MemberProfileEntity.IsProfileVisible"/>) and a hidden profile is returned only
/// when the caller asks for it with <c>includeHidden</c>, which only the owner's own request does.
/// The links the member hid are never returned.
/// </summary>
/// <remarks>
/// The community-activity collections (blog posts, videos, talks, forum posts,
/// packages, meetups) are always returned empty — populating those requires a real
/// external content-aggregation integration, which is out of scope for this provider.
/// Only <see cref="ProfileIdentity"/> (headline, bio, avatar, handle, feeds-as-social-links) and the
/// forum badges the member chose to show (<see cref="ForumBadgeService"/>, empty if the forum can't be reached) are real.
/// The display name, headline and bio are the member's own, falling back to the GitHub
/// name, company and bio respectively.
/// </remarks>
public sealed class MemberProfileDataProvider : IProfileDataProvider
{
    private readonly MemberProfileStore _store;
    private readonly ProfileAvatarUrlResolver _avatarUrlResolver;
    private readonly FeedSubmissionImageProxyService _imageProxy;
    private readonly ForumBadgeService? _forumBadges;

    public MemberProfileDataProvider(
        MemberProfileStore store,
        ProfileAvatarUrlResolver avatarUrlResolver,
        FeedSubmissionImageProxyService imageProxy,
        ForumBadgeService? forumBadges = null)
    {
        _store = store;
        _avatarUrlResolver = avatarUrlResolver;
        _imageProxy = imageProxy;
        _forumBadges = forumBadges;
    }

    public async Task<CommunityProfile?> GetProfileAsync(string slug, bool includeHidden = false, CancellationToken cancellationToken = default)
    {
        var entity = await _store.GetByHandleAsync(slug, cancellationToken);
        if (entity == null
            || entity.OnboardingStatus != OnboardingStatus.Completed
            || (!entity.IsProfileVisible && !includeHidden))
        {
            return null;
        }

        var socialLinks = entity.Feeds
            .Where(f => !f.IsRemoved && !f.IsHidden)
            .OrderBy(f => f.SortOrder)
            .ThenBy(f => f.AddedUtc)
            .ThenBy(f => f.Id)
            .Select(f => new ProfileSocialLink(f.Platform, f.Url, _imageProxy.CreateProxyUrl(f.FaviconUrl)))
            .ToList();

        IReadOnlyList<ProfileBadge> badges = _forumBadges != null
            ? await _forumBadges.GetShownBadgesAsync(entity, cancellationToken)
            : [];

        return new CommunityProfile(
            Slug: entity.GitHubHandle,
            PlatformId: entity.PlatformProfileId,
            Identity: new ProfileIdentity(
                DisplayName: entity.EffectiveDisplayName,
                Headline: entity.EffectiveHeadline,
                Bio: entity.EffectiveBio,
                AvatarUrl: _avatarUrlResolver.Resolve(entity.AvatarMediaKey, entity.GitHubHandle),
                Location: null,
                Pronouns: null,
                IsMvp: false,
                MvpYears: [],
                GitHubHandle: entity.GitHubHandle,
                YouTubeChannelUrl: null,
                SocialLinks: socialLinks,
                HasCustomAvatar: entity.AvatarMediaKey.HasValue)
            {
                HeadlineIsCompany = entity.Headline == null && !string.IsNullOrWhiteSpace(entity.GitHubCompany),
            },
            BlogPosts: [],
            Videos: [],
            ForumPosts: [],
            Packages: [],
            Talks: [],
            Badges: badges,
            SocialPosts: [],
            Meetups: [])
        {
            DisplayChoices = ProfileDisplay.StoredChoices(entity),
        };
    }
}

using UmbracoCommunity.Web.Features.Profiles.Data;
using UmbracoCommunity.Web.Features.Profiles.Data.Entities;
using UmbracoCommunity.Web.Features.Profiles.Models;

namespace UmbracoCommunity.Web.Features.Profiles;

/// <summary>
/// Live profile source backed by <see cref="MemberProfileStore"/>. Replaces
/// <see cref="DummyProfileDataProvider"/> in DI. Only returns a profile for members who
/// have completed onboarding (see <see cref="OnboardingStatus.Completed"/>) — an
/// in-progress onboarding is never visible as a public profile.
/// </summary>
/// <remarks>
/// The community-activity collections (blog posts, videos, talks, badges, forum posts,
/// packages, meetups) are always returned empty — populating those requires a real
/// Sphere content-aggregation integration, which is out of scope for this provider.
/// Only <see cref="ProfileIdentity"/> (bio, avatar, handle, feeds-as-social-links) is real.
/// </remarks>
public sealed class MemberProfileDataProvider : IProfileDataProvider
{
    private readonly MemberProfileStore _store;
    private readonly ProfileAvatarUrlResolver _avatarUrlResolver;

    public MemberProfileDataProvider(MemberProfileStore store, ProfileAvatarUrlResolver avatarUrlResolver)
    {
        _store = store;
        _avatarUrlResolver = avatarUrlResolver;
    }

    public async Task<CommunityProfile?> GetProfileAsync(string slug, CancellationToken cancellationToken = default)
    {
        var entity = await _store.GetByHandleAsync(slug, cancellationToken);
        if (entity == null || entity.OnboardingStatus != OnboardingStatus.Completed)
        {
            return null;
        }

        var socialLinks = entity.Feeds
            .Where(f => !f.IsRemoved && !f.IsHidden)
            .Select(f => new ProfileSocialLink(f.Platform, f.Url))
            .ToList();

        return new CommunityProfile(
            Slug: entity.GitHubHandle,
            SphereId: entity.SphereProfileId,
            Identity: new ProfileIdentity(
                DisplayName: entity.DisplayName,
                Headline: null,
                Bio: entity.Bio,
                AvatarUrl: _avatarUrlResolver.Resolve(entity.AvatarMediaKey, entity.GitHubHandle),
                Location: null,
                Pronouns: null,
                IsMvp: false,
                MvpYears: [],
                GitHubHandle: entity.GitHubHandle,
                YouTubeChannelUrl: null,
                SocialLinks: socialLinks),
            BlogPosts: [],
            Videos: [],
            ForumPosts: [],
            Packages: [],
            Talks: [],
            Badges: [],
            SocialPosts: [],
            Meetups: []);
    }
}

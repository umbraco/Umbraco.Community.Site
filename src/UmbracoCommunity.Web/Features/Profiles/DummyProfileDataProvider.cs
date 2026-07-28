using UmbracoCommunity.Web.Features.Profiles.Models;

namespace UmbracoCommunity.Web.Features.Profiles;

/// <summary>
/// Phase 1 profile source: returns a single, completely fictional profile
/// ("Alex Penrose") for any slug, so the page can be designed and styled against
/// realistic data before the live external platform integration exists. One reserved slug
/// (<see cref="NotFoundSlug"/>) returns <c>null</c> to exercise the 404 path.
/// </summary>
/// <remarks>
/// Nothing here represents a real community member. The shape mirrors the proposed
/// external platform contract (see <c>docs/COMMUNITY_PROFILES_SPEC.md</c> §5.1) so the fixture
/// and the future live payload stay byte-compatible.
/// </remarks>
public sealed class DummyProfileDataProvider : IProfileDataProvider
{
    /// <summary>Visiting <c>/community/profiles/not-found</c> returns no profile (renders a 404).</summary>
    public const string NotFoundSlug = "not-found";

    public Task<CommunityProfile?> GetProfileAsync(string slug, CancellationToken cancellationToken = default)
    {
        if (string.Equals(slug, NotFoundSlug, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<CommunityProfile?>(null);
        }

        return Task.FromResult<CommunityProfile?>(AlexPenrose);
    }

    private const string Handle = "alexpenrose";

    private static readonly CommunityProfile AlexPenrose = new(
        Slug: Handle,
        PlatformId: "9a35e0df-abcd-496f-a7e7-68aca0d03ea2",
        Identity: new ProfileIdentity(
            DisplayName: "Alex Penrose",
            Headline: "Full-stack Umbraco developer & package author",
            Bio: "Building things with Umbraco since v7. Loves block editors, tidy CSS, and a good content model.",
            // For a real member this is their GitHub avatar (https://github.com/{handle}.png),
            // derived from the handle. "alexpenrose" is fictional, so the fixture uses a
            // Pexels stock portrait (CSP-allowed) as a placeholder.
            AvatarUrl: "https://images.pexels.com/photos/5530440/pexels-photo-5530440.jpeg?auto=compress&cs=tinysrgb&w=400",
            Location: "Bristol, UK",
            Pronouns: "they/them",
            IsMvp: true,
            MvpYears: new[] { 2022, 2023, 2024, 2025, 2026 },
            GitHubHandle: Handle,
            YouTubeChannelUrl: "https://www.youtube.com/@alexpenrose",
            SocialLinks: new[]
            {
                new ProfileSocialLink("Mastodon", "https://umbracocommunity.social/@alexpenrose"),
                new ProfileSocialLink("LinkedIn", "https://www.linkedin.com/in/alexpenrose"),
                new ProfileSocialLink("Bluesky", "https://bsky.app/profile/alexpenrose.bsky.social"),
            },
            HasCustomAvatar: true),
        BlogPosts: new[]
        {
            new ProfileBlogPost(
                "Taming the Block Grid editor",
                "https://example.dev/blog/block-grid",
                new DateTimeOffset(2026, 5, 2, 9, 0, 0, TimeSpan.Zero),
                "alexpenrose.dev",
                "Layouts, areas and the mental model that finally made the Block Grid click for me."),
            new ProfileBlogPost(
                "Multi-tenant Umbraco the easy way",
                "https://example.dev/blog/multi-tenant",
                new DateTimeOffset(2026, 3, 18, 9, 0, 0, TimeSpan.Zero),
                "alexpenrose.dev",
                "Running many sites from one Umbraco install without hardcoding a single root node."),
            new ProfileBlogPost(
                "What's new in Umbraco 17",
                "https://umbraco.com/blog/whats-new-17",
                new DateTimeOffset(2026, 2, 10, 9, 0, 0, TimeSpan.Zero),
                "Umbraco HQ",
                "A whistle-stop tour of the headline features landing in the v17 release."),
            new ProfileBlogPost(
                "Writing custom property editors in v17",
                "https://example.dev/blog/property-editors-v17",
                new DateTimeOffset(2026, 1, 27, 9, 0, 0, TimeSpan.Zero),
                "alexpenrose.dev",
                "Building a property editor with the new back-office APIs, from manifest to data type."),
            new ProfileBlogPost(
                "A pragmatic guide to Umbraco caching",
                "https://example.dev/blog/caching",
                new DateTimeOffset(2025, 11, 9, 9, 0, 0, TimeSpan.Zero),
                "24 days in Umbraco",
                "Output cache, content cache and HTTP caching — when to reach for each, with examples."),
            new ProfileBlogPost(
                "Guest post: scaling the community site",
                "https://umbraco.com/blog/scaling-community",
                new DateTimeOffset(2025, 9, 30, 9, 0, 0, TimeSpan.Zero),
                "Umbraco HQ",
                "Behind the scenes of the infrastructure and content model powering community.umbraco.com."),
        },
        Videos: new[]
        {
            // ThumbnailUrl null for now: the YouTube image host isn't in the CSP
            // allow-list and the fixture has no real video ids. Real thumbnails need
            // both before they'll render.
            new ProfileVideo(
                "Umbraco package dev livestream",
                "https://www.youtube.com/watch?v=abc123",
                null,
                new DateTimeOffset(2026, 4, 20, 17, 0, 0, TimeSpan.Zero),
                "A two-hour livestream building a package from an empty folder to a Marketplace release."),
            new ProfileVideo(
                "Block Grid deep dive",
                "https://www.youtube.com/watch?v=def456",
                null,
                new DateTimeOffset(2026, 2, 14, 17, 0, 0, TimeSpan.Zero),
                "Everything you can do with the Block Grid editor, with live examples."),
            new ProfileVideo(
                "Building a Vite + Umbraco frontend",
                "https://www.youtube.com/watch?v=ghi789",
                null,
                new DateTimeOffset(2025, 12, 2, 17, 0, 0, TimeSpan.Zero),
                "Wiring up Vite, TypeScript and Lit components in an Umbraco site."),
        },
        ForumPosts: new[]
        {
            new ProfileForumPost(
                "Re: Block list custom view not rendering",
                "https://our.umbraco.com/forum/123#comment-456",
                new DateTimeOffset(2026, 6, 1, 14, 30, 0, TimeSpan.Zero),
                4),
            new ProfileForumPost(
                "Re: Models Builder out of sync after upgrade",
                "https://our.umbraco.com/forum/124#comment-981",
                new DateTimeOffset(2026, 5, 12, 10, 5, 0, TimeSpan.Zero),
                7),
            new ProfileForumPost(
                "Re: Best approach for multi-tenant 404s",
                "https://our.umbraco.com/forum/125#comment-1202",
                new DateTimeOffset(2026, 4, 3, 16, 20, 0, TimeSpan.Zero),
                12),
        },
        Packages: new[]
        {
            new ProfilePackage(
                "Penrose.Seo",
                "https://marketplace.umbraco.com/package/penrose.seo",
                new[] { "3.2.1", "3.1.0", "2.4.0" },
                "SEO toolkit: sitemaps, meta tags and structured data for Umbraco."),
            new ProfilePackage(
                "Penrose.BlockHelpers",
                "https://marketplace.umbraco.com/package/penrose.blockhelpers",
                new[] { "1.4.0", "1.3.2" },
                "Handy helpers and base classes for working with the block editors."),
            new ProfilePackage(
                "Penrose.Redirects",
                "https://marketplace.umbraco.com/package/penrose.redirects",
                new[] { "2.0.1", "2.0.0", "1.0.0" },
                "Manage 301/302 redirects from the Umbraco back office."),
        },
        Talks: new[]
        {
            new ProfileTalk(
                "Designing for the long tail",
                "Codegarden 2026",
                new DateTimeOffset(2026, 6, 11, 13, 45, 0, TimeSpan.Zero),
                "https://sessionize.com/s/alex/designing-long-tail",
                "How to build community features that serve thousands of contributors, not just the headline few."),
            new ProfileTalk(
                "Block editors, end to end",
                "Umbraco UK Festival 2025",
                new DateTimeOffset(2025, 11, 7, 11, 0, 0, TimeSpan.Zero),
                "https://sessionize.com/s/alex/block-editors",
                "A practical tour of the Block List and Block Grid editors, from element types to custom views."),
            new ProfileTalk(
                "Shipping your first package",
                "Umbraco Spark 2025",
                new DateTimeOffset(2025, 3, 21, 14, 30, 0, TimeSpan.Zero),
                "https://sessionize.com/s/alex/first-package",
                "Everything that happens after the code works: packaging, versioning and supporting your users."),
        },
        Badges: new[]
        {
            // IconUrl left null for now: the real forum badge icons live on
            // our.umbraco.com, which isn't in the CSP image allow-list, so they'd be
            // blocked. Badges render as name tokens until icons are served from an
            // allow-listed host (or inlined as data: URIs).
            new ProfileBadge(
                "Core Contributor",
                null,
                "Made a PR to the Umbraco source code",
                new DateTimeOffset(2023, 6, 1, 0, 0, 0, TimeSpan.Zero)),
            new ProfileBadge(
                "Package Owner",
                null,
                "Maintains package(s) on the Marketplace",
                new DateTimeOffset(2023, 1, 15, 0, 0, 0, TimeSpan.Zero)),
            new ProfileBadge(
                "Forum Contributor",
                null,
                "Active helper on the community forum",
                new DateTimeOffset(2022, 4, 8, 0, 0, 0, TimeSpan.Zero)),
            new ProfileBadge(
                "Bug Squasher",
                null,
                "Reported and helped fix CMS bugs",
                new DateTimeOffset(2022, 9, 10, 0, 0, 0, TimeSpan.Zero)),
            new ProfileBadge(
                "Meet-up Organiser",
                null,
                "Organises local Umbraco meet-ups",
                new DateTimeOffset(2024, 2, 20, 0, 0, 0, TimeSpan.Zero)),
            new ProfileBadge(
                "Conference Speaker",
                null,
                "Has spoken at an Umbraco conference",
                new DateTimeOffset(2025, 6, 12, 0, 0, 0, TimeSpan.Zero)),
            new ProfileBadge(
                "Published Author",
                null,
                "Written articles or a book about Umbraco",
                new DateTimeOffset(2024, 10, 1, 0, 0, 0, TimeSpan.Zero)),
        },
        SocialPosts: new[]
        {
            new ProfileSocialPost(
                "LinkedIn",
                "https://www.linkedin.com/posts/alexpenrose-codegarden",
                "Thrilled to be speaking at Codegarden 2026 about designing for the long tail. Come say hi! 🎤",
                new DateTimeOffset(2026, 6, 9, 8, 30, 0, TimeSpan.Zero)),
            new ProfileSocialPost(
                "Mastodon",
                "https://umbracocommunity.social/@alexpenrose/112233",
                "Just shipped Penrose.Seo 3.2.1 — automatic XML sitemaps out of the box. 🎉",
                new DateTimeOffset(2026, 5, 4, 12, 15, 0, TimeSpan.Zero)),
            new ProfileSocialPost(
                "Bluesky",
                "https://bsky.app/profile/alexpenrose.bsky.social/post/abc",
                "Hot take: the Block Grid editor is the best thing to happen to Umbraco content modelling in years.",
                new DateTimeOffset(2026, 4, 22, 18, 5, 0, TimeSpan.Zero)),
            new ProfileSocialPost(
                "LinkedIn",
                "https://www.linkedin.com/posts/alexpenrose-mvp",
                "Honoured to be renewed as an Umbraco MVP for 2026 — thank you to this brilliant community. 💙",
                new DateTimeOffset(2026, 1, 30, 9, 0, 0, TimeSpan.Zero)),
        },
        Meetups: new[]
        {
            new ProfileMeetup(
                "RSVP'd yes to",
                "UmbLeeds: Codegarden review",
                "Umbraco Leeds Meetup group",
                new DateTimeOffset(2026, 6, 18, 18, 0, 0, TimeSpan.Zero),
                "https://www.meetup.com/umbraco-leeds/"),
            new ProfileMeetup(
                "Event hosted:",
                "UMBRAAD",
                "Umbraco Virtual",
                new DateTimeOffset(2026, 5, 21, 18, 0, 0, TimeSpan.Zero),
                null),
            new ProfileMeetup(
                "Attended",
                "Umbraco UK Festival meetup",
                "Bristol Umbraco Meetup group",
                new DateTimeOffset(2026, 3, 12, 18, 30, 0, TimeSpan.Zero),
                null),
        });
}

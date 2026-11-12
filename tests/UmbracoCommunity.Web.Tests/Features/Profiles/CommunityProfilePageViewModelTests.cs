using FluentAssertions;
using Moq;
using Umbraco.Cms.Core.Models.PublishedContent;
using UmbracoCommunity.Web.Features.Profiles;
using UmbracoCommunity.Web.Features.Profiles.Models;
using UmbracoCommunity.Web.Models.Pages;
using UmbracoCommunity.Web.Models.PublishedModels;
using Xunit;

namespace UmbracoCommunity.Web.Tests.Features.Profiles;

/// <summary>When the profile page shows the DevRel banner, and which sections and items it marks as hidden.</summary>
public class CommunityProfilePageViewModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly CommunityProfile Profile = new(
        Slug: "octocat",
        PlatformId: null,
        Identity: new ProfileIdentity("The Octocat", null, null, null, null, null, false, [], "octocat", null, [], false),
        BlogPosts:
        [
            new ProfileBlogPost("First post", "https://example.dev/1", Now, null, ActivityId: "b1"),
            new ProfileBlogPost("Second post", "https://example.dev/2", Now, null, ActivityId: "b2"),
        ],
        Videos: [new ProfileVideo("A video", "https://video.example/1", null, Now, ActivityId: "v1")],
        ForumPosts: [],
        Packages: [],
        Talks: [],
        Badges: [],
        SocialPosts: [],
        Meetups: []);

    private static readonly ProfileDisplayChoices HidesSomething =
        new([PlatformActivityGroups.Videos], ["b1"], []);

    [Fact]
    public void Nobody_but_DevRel_gets_the_banner()
    {
        var model = Model(Profile with { DisplayChoices = HidesSomething }, isDevRelView: false, isHiddenFromPublic: true, isGated: true, showEverything: true, hasHiddenSocialLinks: true);

        model.ShowDevRelBanner.Should().BeFalse();
    }

    [Fact]
    public void DevRel_gets_no_banner_when_the_page_is_what_the_public_sees()
    {
        Model(Profile, isDevRelView: true).ShowDevRelBanner.Should().BeFalse();
    }

    [Fact]
    public void DevRel_gets_the_banner_for_a_hidden_profile()
    {
        Model(Profile, isDevRelView: true, isHiddenFromPublic: true).ShowDevRelBanner.Should().BeTrue();
    }

    [Fact]
    public void DevRel_gets_the_banner_when_the_sections_are_not_open_to_the_public()
    {
        Model(Profile, isDevRelView: true, isGated: true).ShowDevRelBanner.Should().BeTrue();
    }

    [Fact]
    public void DevRel_gets_the_banner_when_the_member_hid_something()
    {
        Model(Profile with { DisplayChoices = HidesSomething }, isDevRelView: true).ShowDevRelBanner.Should().BeTrue();
    }

    [Fact]
    public void DevRel_always_gets_the_banner_when_everything_is_shown()
    {
        Model(Profile, isDevRelView: true, showEverything: true).ShowDevRelBanner.Should().BeTrue();
    }

    [Fact]
    public void Hidden_items_are_shown_and_marked_only_when_everything_is_shown()
    {
        var profile = Profile with { DisplayChoices = HidesSomething };

        var everything = Model(ProfileDisplay.Apply(profile, showHidden: true), isDevRelView: true, showEverything: true);
        var asPublic = Model(ProfileDisplay.Apply(profile), isDevRelView: true);

        everything.Profile!.BlogPosts.Select(p => p.ActivityId).Should().Equal("b1", "b2");
        everything.Profile.Videos.Should().ContainSingle();
        everything.IsMarkedHidden(PlatformActivityGroups.Blog, "b1").Should().BeTrue();
        everything.IsMarkedHidden(PlatformActivityGroups.Blog, "b2").Should().BeFalse();
        everything.IsMarkedHidden(PlatformActivityGroups.Blog).Should().BeFalse();
        everything.IsMarkedHidden(PlatformActivityGroups.Videos).Should().BeTrue();

        asPublic.Profile!.BlogPosts.Select(p => p.ActivityId).Should().Equal("b2");
        asPublic.Profile.Videos.Should().BeEmpty();
        asPublic.IsMarkedHidden(PlatformActivityGroups.Blog, "b1").Should().BeFalse();
        asPublic.IsMarkedHidden(PlatformActivityGroups.Videos).Should().BeFalse();
    }

    [Fact]
    public void DevRel_gets_the_banner_when_the_member_hid_a_link()
    {
        Model(Profile, isDevRelView: true, hasHiddenSocialLinks: true).ShowDevRelBanner.Should().BeTrue();
    }

    [Fact]
    public void Hidden_links_are_marked_only_when_everything_is_shown()
    {
        var hidden = new ProfileSocialLink("Mastodon", "https://mastodon.example/@octocat", IsHiddenByMember: true);
        var shown = new ProfileSocialLink("Blog", "https://example.dev");

        var everything = Model(Profile, isDevRelView: true, showEverything: true, hasHiddenSocialLinks: true);
        var asPublic = Model(Profile, isDevRelView: true, hasHiddenSocialLinks: true);

        everything.IsMarkedHidden(hidden).Should().BeTrue();
        everything.IsMarkedHidden(shown).Should().BeFalse();
        asPublic.IsMarkedHidden(hidden).Should().BeFalse();
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(PlatformActivityGroups.Blog, true)]
    [InlineData(PlatformActivityGroups.Forum, false)]
    public void The_timeline_reports_whether_anything_matches_the_type_filter(string? type, bool expected)
    {
        var timeline = ProfileTimeline.ByMonth(ProfileTimeline.Entries(Profile));

        var contentType = new Mock<IPublishedContentType>();
        contentType.Setup(t => t.Alias).Returns(CommunityProfilePage.ModelTypeAlias);
        var page = new Mock<IPublishedContent>();
        page.Setup(p => p.ContentType).Returns(contentType.Object);

        var filtered = new CommunityProfilePageViewModel(page.Object)
        {
            Handle = "octocat",
            Profile = Profile,
            Timeline = timeline,
            TimelineTypes = ProfileTimeline.Types(timeline.SelectMany(m => m.Entries)),
            TimelineType = type,
        };

        filtered.TimelineHasShownEntries.Should().Be(expected);
    }

    private static CommunityProfilePageViewModel Model(
        CommunityProfile? profile,
        bool isDevRelView,
        bool isHiddenFromPublic = false,
        bool isGated = false,
        bool showEverything = false,
        bool hasHiddenSocialLinks = false)
    {
        var contentType = new Mock<IPublishedContentType>();
        contentType.Setup(t => t.Alias).Returns(CommunityProfilePage.ModelTypeAlias);
        var page = new Mock<IPublishedContent>();
        page.Setup(p => p.ContentType).Returns(contentType.Object);
        page.Setup(p => p.Name).Returns("Profile");

        return new CommunityProfilePageViewModel(page.Object)
        {
            Handle = "octocat",
            Profile = profile,
            IsDevRelView = isDevRelView,
            IsHiddenFromPublic = isHiddenFromPublic,
            IsGated = isGated,
            IsShowingEverything = showEverything,
            HasHiddenSocialLinks = hasHiddenSocialLinks,
        };
    }
}

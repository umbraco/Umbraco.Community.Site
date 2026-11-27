using FluentAssertions;
using UmbracoCommunity.Web.Features.Profiles;
using UmbracoCommunity.Web.Features.Profiles.Models;
using Xunit;

namespace UmbracoCommunity.Web.Tests.Features.Profiles;

public class ProfileSeoTests
{
    private static CommunityProfile Profile(
        string? headline = "Umbraco developer",
        string? bio = "Writes about Umbraco.",
        params ProfileSocialLink[] links) => new(
        Slug: "octocat",
        PlatformId: null,
        Identity: new ProfileIdentity("The Octocat", headline, bio, "https://github.com/octocat.png", null, "they/them",
            false, [], "octocat", null, links, false),
        BlogPosts: [], Videos: [], ForumPosts: [], Packages: [], Talks: [], Badges: [], SocialPosts: [], Meetups: []);

    private static readonly EnrichedSectionsAccess Open = new(Show: true, IsGated: false);

    [Fact]
    public void A_shown_profile_with_open_sections_is_publicly_visible()
    {
        ProfileSeo.IsPubliclyVisible(new ViewableProfile(Profile(), false), Open).Should().BeTrue();
    }

    [Fact]
    public void Gated_sections_keep_the_page_out()
    {
        ProfileSeo.IsPubliclyVisible(new ViewableProfile(Profile(), false), new(true, true)).Should().BeFalse();
        ProfileSeo.IsPubliclyVisible(new ViewableProfile(Profile(), false), EnrichedSectionsAccess.None).Should().BeFalse();
    }

    [Fact]
    public void Hidden_previews_and_missing_profiles_are_not_visible()
    {
        ProfileSeo.IsPubliclyVisible(new ViewableProfile(Profile(), IsHiddenPreview: true), Open).Should().BeFalse();
        ProfileSeo.IsPubliclyVisible(ViewableProfile.None, Open).Should().BeFalse();
    }

    [Fact]
    public void Description_joins_the_headline_and_bio_and_is_cut_to_fit()
    {
        ProfileSeo.Description(Profile().Identity).Should().Be("Umbraco developer · Writes about Umbraco.");
        ProfileSeo.Description(Profile(headline: " ").Identity).Should().Be("Writes about Umbraco.");
        ProfileSeo.Description(Profile(headline: null, bio: null).Identity).Should().BeEmpty();
        ProfileSeo.Description(Profile(headline: new string('a', 500)).Identity).Should().HaveLength(200).And.EndWith("...");
    }

    [Fact]
    public void The_headline_is_used_exactly_as_the_member_wrote_it()
    {
        // The headline is always the member's own text (even when it started as their GitHub company), so it is not
        // dressed up as a company.
        ProfileSeo.Description(Profile(headline: "acme widgets GmbH").Identity).Should().Be("acme widgets GmbH · Writes about Umbraco.");
    }

    [Fact]
    public void Share_image_is_a_small_square()
    {
        static ProfileIdentity With(string url, bool custom) =>
            Profile().Identity with { AvatarUrl = url, HasCustomAvatar = custom };
        static string Resize(string u, int size) => $"{u}?width={size}&hmac=x";

        ProfileSeo.ShareImageUrl(With("/media/a/me.png", true), Resize).Should().Be("/media/a/me.png?width=600&hmac=x");
        ProfileSeo.ShareImageUrl(With("/media/a/me.png?width=512&height=512&hmac=y", true), Resize).Should().Be("/media/a/me.png?width=600&hmac=x");
        ProfileSeo.ShareImageUrl(With("/media/a/me.svg", true), Resize).Should().Be("/media/a/me.svg");
        ProfileSeo.ShareImageUrl(With("https://github.com/octocat.png", false), Resize).Should().Be("https://github.com/octocat.png?size=600");
        ProfileSeo.ShareImageUrl(With("", false), Resize).Should().BeNull();
    }

    [Fact]
    public void Schema_has_the_name_and_only_https_links()
    {
        var profile = Profile(links:
        [
            new ProfileSocialLink("Blog", "https://example.com/me"),
            new ProfileSocialLink("Old", "http://example.com/old"),
        ]);

        var json = ProfileSeo.Schema(profile, new Uri("https://x.test/p/octocat/"), new Uri("https://github.com/octocat.png")).ToString();

        json.Should().Contain("ProfilePage").And.Contain("The Octocat").And.Contain("https://example.com/me");
        json.Should().NotContain("example.com/old").And.NotContain("they/them");
    }
}

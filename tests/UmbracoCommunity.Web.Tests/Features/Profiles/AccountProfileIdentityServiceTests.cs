using FluentAssertions;
using Moq;
using UmbracoCommunity.Web.Features.OpenBadges.Infrastructure;
using UmbracoCommunity.Web.Features.OpenBadges.Models;
using UmbracoCommunity.Web.Features.Profiles;
using UmbracoCommunity.Web.Features.Profiles.Models;
using Xunit;

namespace UmbracoCommunity.Web.Tests.Features.Profiles;

public class AccountProfileIdentityServiceTests
{
    private const string Handle = "octocat";

    private static CommunityProfile Profile() => new(
        Handle,
        null,
        new ProfileIdentity(
            "The Octocat", "Developer", "Hello", "/avatar.png", null, null, false, [], Handle, null,
            [new ProfileSocialLink("Mastodon", "https://social.example/@octocat", "/proxy?token=m")], false),
        [], [], [], [], [], [], [], []);

    private static AccountProfileIdentityService Create(CommunityProfile? profile, MvpStatus mvp, Mock<IProfileDataProvider>? provider = null)
    {
        provider ??= new Mock<IProfileDataProvider>();
        provider
            .Setup(p => p.GetProfileAsync(Handle, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);
        var eligibility = new Mock<IMvpBadgeEligibility>();
        eligibility.Setup(e => e.GetStatusAsync(Handle, It.IsAny<CancellationToken>())).ReturnsAsync(mvp);
        return new AccountProfileIdentityService(provider.Object, eligibility.Object);
    }

    [Fact]
    public async Task Returns_what_the_public_profile_shows_including_a_hidden_profile()
    {
        var provider = new Mock<IProfileDataProvider>();
        var identity = await Create(Profile(), MvpStatus.None, provider).GetAsync(Handle);

        identity.Should().NotBeNull();
        identity!.DisplayName.Should().Be("The Octocat");
        identity.Headline.Should().Be("Developer");
        identity.Bio.Should().Be("Hello");
        identity.SocialLinks.Should().Equal(new ProfileSocialLink("Mastodon", "https://social.example/@octocat", "/proxy?token=m"));
        identity.IsMvp.Should().BeFalse();
        provider.Verify(p => p.GetProfileAsync(Handle, true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Adds_the_members_mvp_years()
    {
        var identity = await Create(Profile(), new MvpStatus([2026, 2025], true)).GetAsync(Handle);

        identity!.IsMvp.Should().BeTrue();
        identity.MvpYears.Should().Equal(2026, 2025);
        identity.IsCurrentMvp.Should().BeTrue();
    }

    [Fact]
    public async Task The_header_carries_the_badges_the_profile_shows()
    {
        var badge = new ProfileBadge("Umbraco MVP", "/proxy?token=b", "An Umbraco MVP", null, "Gold");
        var header = await Create(Profile() with { Badges = [badge] }, MvpStatus.None).GetHeaderAsync(Handle);

        header!.Identity.DisplayName.Should().Be("The Octocat");
        header.Badges.Should().Equal(badge);
    }

    [Fact]
    public async Task Is_null_without_a_profile()
    {
        (await Create(null, new MvpStatus([2026], true)).GetAsync(Handle)).Should().BeNull();
    }
}

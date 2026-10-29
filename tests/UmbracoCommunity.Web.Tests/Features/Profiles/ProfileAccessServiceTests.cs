using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Umbraco.Cms.Core.Security;
using UmbracoCommunity.Web.Features.Profiles;
using UmbracoCommunity.Web.Features.Profiles.Models;
using Xunit;

namespace UmbracoCommunity.Web.Tests.Features.Profiles;

public class ProfileAccessServiceTests
{
    private const string Handle = "octocat";

    private static readonly CommunityProfile HiddenProfile = new(
        Slug: Handle,
        PlatformId: null,
        Identity: new ProfileIdentity(
            DisplayName: "The Octocat",
            Headline: null,
            Bio: null,
            AvatarUrl: "https://github.com/octocat.png",
            Location: null,
            Pronouns: null,
            IsMvp: false,
            MvpYears: [],
            GitHubHandle: Handle,
            YouTubeChannelUrl: null,
            SocialLinks: [],
            HasCustomAvatar: false),
        BlogPosts: [],
        Videos: [],
        ForumPosts: [],
        Packages: [],
        Talks: [],
        Badges: [],
        SocialPosts: [],
        Meetups: []);

    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(false, false, true, false)]
    [InlineData(false, true, false, false)]
    [InlineData(false, true, true, true)]
    [InlineData(true, false, false, true)]
    [InlineData(true, false, true, true)]
    [InlineData(true, true, false, true)]
    [InlineData(true, true, true, true)]
    public void CanSeeEnrichedSections_follows_the_decision_table(
        bool openToAll, bool isOwnProfile, bool isInEarlyAccess, bool expected)
    {
        ProfileAccessService.CanSeeEnrichedSections(openToAll, isOwnProfile, isInEarlyAccess)
            .Should().Be(expected);
    }

    [Fact]
    public void Early_access_group_name_matches_the_backoffice_group()
    {
        ProfileAccessService.EarlyAccessGroupName.Should().Be("Early access");
    }

    [Theory]
    [InlineData("octocat")]
    [InlineData("OctoCat")]
    public async Task The_owner_gets_a_preview_of_their_hidden_profile(string signedInHandle)
    {
        var (service, provider) = CreateService(signedInHandle);

        var viewable = await service.GetViewableProfileAsync(Handle);

        viewable.Profile.Should().BeSameAs(HiddenProfile);
        viewable.IsHiddenPreview.Should().BeTrue();
        provider.Verify(p => p.GetProfileAsync(Handle, true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("hubot")]
    public async Task Anyone_else_gets_nothing_for_a_hidden_profile(string? signedInHandle)
    {
        var (service, provider) = CreateService(signedInHandle);

        var viewable = await service.GetViewableProfileAsync(Handle);

        viewable.Should().Be(ViewableProfile.None);
        provider.Verify(p => p.GetProfileAsync(It.IsAny<string>(), true, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_visible_profile_is_not_a_preview_even_for_its_owner()
    {
        var provider = new Mock<IProfileDataProvider>();
        provider
            .Setup(p => p.GetProfileAsync(Handle, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(HiddenProfile);
        var service = new ProfileAccessService(MemberManagerFor(Handle).Object, provider.Object, NullLogger<ProfileAccessService>.Instance);

        var viewable = await service.GetViewableProfileAsync(Handle);

        viewable.Profile.Should().BeSameAs(HiddenProfile);
        viewable.IsHiddenPreview.Should().BeFalse();
    }

    [Fact]
    public async Task A_failing_member_lookup_is_treated_as_anonymous()
    {
        var memberManager = new Mock<IMemberManager>();
        memberManager.Setup(m => m.IsLoggedIn()).Returns(true);
        memberManager.Setup(m => m.GetCurrentMemberAsync()).ThrowsAsync(new InvalidOperationException());
        var provider = HiddenProfileProvider();
        var service = new ProfileAccessService(memberManager.Object, provider.Object, NullLogger<ProfileAccessService>.Instance);

        var viewable = await service.GetViewableProfileAsync(Handle);

        viewable.Should().Be(ViewableProfile.None);
    }

    private static (ProfileAccessService Service, Mock<IProfileDataProvider> Provider) CreateService(string? signedInHandle)
    {
        var provider = HiddenProfileProvider();
        var service = new ProfileAccessService(
            MemberManagerFor(signedInHandle).Object,
            provider.Object,
            NullLogger<ProfileAccessService>.Instance);
        return (service, provider);
    }

    // A provider holding one hidden profile: returned only when hidden profiles are requested.
    private static Mock<IProfileDataProvider> HiddenProfileProvider()
    {
        var provider = new Mock<IProfileDataProvider>();
        provider
            .Setup(p => p.GetProfileAsync(Handle, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CommunityProfile?)null);
        provider
            .Setup(p => p.GetProfileAsync(Handle, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(HiddenProfile);
        return provider;
    }

    private static Mock<IMemberManager> MemberManagerFor(string? signedInHandle)
    {
        var memberManager = new Mock<IMemberManager>();
        memberManager.Setup(m => m.IsLoggedIn()).Returns(signedInHandle != null);
        memberManager
            .Setup(m => m.GetCurrentMemberAsync())
            .ReturnsAsync(signedInHandle != null ? new MemberIdentityUser { UserName = signedInHandle } : null);
        return memberManager;
    }
}

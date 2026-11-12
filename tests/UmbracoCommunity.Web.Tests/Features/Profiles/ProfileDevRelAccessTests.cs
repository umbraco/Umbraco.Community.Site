using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Umbraco.Cms.Core.Security;
using UmbracoCommunity.Web.Features.Profiles;
using UmbracoCommunity.Web.Features.Profiles.Models;
using Xunit;

namespace UmbracoCommunity.Web.Tests.Features.Profiles;

/// <summary>
/// What a member of the DevRel member group may see on a profile page, next to everyone else: anonymous visitors,
/// other members, and early access members.
/// </summary>
public class ProfileDevRelAccessTests
{
    private const string Handle = "octocat";
    private const string DevRelHandle = "devrel-dee";

    private static readonly CommunityProfile Profile = new(
        Slug: Handle,
        PlatformId: null,
        Identity: new ProfileIdentity("The Octocat", null, null, null, null, null, false, [], Handle, null, [], false),
        BlogPosts: [],
        Videos: [],
        ForumPosts: [],
        Packages: [],
        Talks: [],
        Badges: [],
        SocialPosts: [],
        Meetups: []);

    public enum Viewer
    {
        Anonymous,
        OtherMember,
        EarlyAccessMember,
        EarlyAccessOwner,
        DevRel,
        DevRelOwner,
    }

    [Fact]
    public void DevRel_group_name_matches_the_backoffice_group()
    {
        ProfileAccessService.DevRelGroupName.Should().Be("DevRel");
    }

    [Theory]
    [InlineData(Viewer.Anonymous)]
    [InlineData(Viewer.OtherMember)]
    [InlineData(Viewer.EarlyAccessMember)]
    public async Task A_hidden_profile_is_not_found_for_anyone_but_its_owner_and_DevRel(Viewer viewer)
    {
        var provider = HiddenProfileProvider();
        var service = CreateService(viewer, provider);

        var viewable = await service.GetViewableProfileAsync(Handle);

        viewable.Should().Be(ViewableProfile.None);
        provider.Verify(p => p.GetProfileAsync(It.IsAny<string>(), true, It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task The_owner_still_gets_a_preview_of_their_hidden_profile()
    {
        var service = CreateService(Viewer.EarlyAccessOwner, HiddenProfileProvider());

        var viewable = await service.GetViewableProfileAsync(Handle);

        viewable.Profile.Should().BeSameAs(Profile);
        viewable.IsHiddenPreview.Should().BeTrue();
        viewable.IsHiddenFromPublic.Should().BeFalse();
    }

    [Fact]
    public async Task DevRel_sees_a_hidden_profile_marked_as_hidden_from_the_public()
    {
        var service = CreateService(Viewer.DevRel, HiddenProfileProvider());

        var viewable = await service.GetViewableProfileAsync(Handle);

        viewable.Profile.Should().BeSameAs(Profile);
        viewable.IsHiddenPreview.Should().BeFalse();
        viewable.IsHiddenFromPublic.Should().BeTrue();
    }

    [Fact]
    public async Task DevRel_sees_a_visible_profile_as_it_is()
    {
        var provider = new Mock<IProfileDataProvider>();
        provider.Setup(p => p.GetProfileAsync(Handle, false, It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(Profile);
        var service = CreateService(Viewer.DevRel, provider);

        var viewable = await service.GetViewableProfileAsync(Handle);

        viewable.Should().Be(new ViewableProfile(Profile, IsHiddenPreview: false));
    }

    [Fact]
    public async Task DevRel_gets_nothing_for_a_handle_without_a_profile()
    {
        var provider = new Mock<IProfileDataProvider>();
        var service = CreateService(Viewer.DevRel, provider);

        var viewable = await service.GetViewableProfileAsync(Handle);

        viewable.Should().Be(ViewableProfile.None);
    }

    [Fact]
    public async Task A_DevRel_view_of_a_hidden_profile_does_not_leak_to_a_later_anonymous_request()
    {
        // One provider, as in the app; each request gets its own scoped ProfileAccessService.
        var provider = HiddenProfileProvider();

        var devRelView = await CreateService(Viewer.DevRel, provider).GetViewableProfileAsync(Handle);
        var anonymousView = await CreateService(Viewer.Anonymous, provider).GetViewableProfileAsync(Handle);

        devRelView.Profile.Should().NotBeNull();
        anonymousView.Should().Be(ViewableProfile.None);
    }

    [Fact]
    public async Task A_DevRel_view_of_a_hidden_profile_is_logged_once_with_both_handles()
    {
        var logger = new CapturingLogger<ProfileAccessService>();
        var service = CreateService(Viewer.DevRel, HiddenProfileProvider(), logger);

        await service.GetViewableProfileAsync(Handle);
        await service.GetViewableProfileAsync(Handle);

        var entry = logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Information).Subject;
        entry.Message.Should().Contain(Handle).And.Contain(DevRelHandle);
    }

    [Theory]
    [InlineData(Viewer.Anonymous)]
    [InlineData(Viewer.OtherMember)]
    [InlineData(Viewer.EarlyAccessOwner)]
    public async Task Nothing_is_logged_for_anyone_else(Viewer viewer)
    {
        var logger = new CapturingLogger<ProfileAccessService>();
        var service = CreateService(viewer, HiddenProfileProvider(), logger);

        await service.GetViewableProfileAsync(Handle);

        logger.Entries.Should().NotContain(e => e.Level == LogLevel.Information);
    }

    [Theory]
    [InlineData(Viewer.Anonymous, false, false)]
    [InlineData(Viewer.OtherMember, false, false)]
    [InlineData(Viewer.EarlyAccessMember, false, false)]
    [InlineData(Viewer.EarlyAccessOwner, true, true)]
    [InlineData(Viewer.DevRel, true, true)]
    [InlineData(Viewer.DevRelOwner, true, true)]
    public async Task With_the_sections_closed_to_the_public_only_early_access_owners_and_DevRel_see_them(
        Viewer viewer, bool expectedShow, bool expectedGated)
    {
        var service = CreateService(viewer, HiddenProfileProvider());

        var access = await service.ResolveAsync(openToAll: false, Handle);

        access.Should().Be(new EnrichedSectionsAccess(expectedShow, expectedGated));
    }

    [Theory]
    [InlineData(Viewer.Anonymous)]
    [InlineData(Viewer.OtherMember)]
    [InlineData(Viewer.EarlyAccessOwner)]
    [InlineData(Viewer.DevRel)]
    public async Task With_the_sections_open_to_all_everyone_sees_them_ungated(Viewer viewer)
    {
        var service = CreateService(viewer, HiddenProfileProvider());

        var access = await service.ResolveAsync(openToAll: true, Handle);

        access.Should().Be(new EnrichedSectionsAccess(Show: true, IsGated: false));
    }

    [Theory]
    [InlineData(Viewer.Anonymous, false)]
    [InlineData(Viewer.OtherMember, false)]
    [InlineData(Viewer.EarlyAccessMember, false)]
    [InlineData(Viewer.EarlyAccessOwner, false)]
    [InlineData(Viewer.DevRelOwner, false)]
    [InlineData(Viewer.DevRel, true)]
    public async Task Only_DevRel_on_someone_elses_profile_is_a_DevRel_view(Viewer viewer, bool expected)
    {
        var service = CreateService(viewer, HiddenProfileProvider());

        (await service.IsDevRelViewAsync(Handle)).Should().Be(expected);
    }

    [Fact]
    public async Task A_failing_group_lookup_is_not_DevRel()
    {
        var member = new MemberIdentityUser { UserName = DevRelHandle };
        var memberManager = new Mock<IMemberManager>();
        memberManager.Setup(m => m.IsLoggedIn()).Returns(true);
        memberManager.Setup(m => m.GetCurrentMemberAsync()).ReturnsAsync(member);
        memberManager.Setup(m => m.GetRolesAsync(member)).ThrowsAsync(new InvalidOperationException());
        var service = new ProfileAccessService(memberManager.Object, HiddenProfileProvider().Object, NullLogger<ProfileAccessService>.Instance);

        (await service.IsCurrentMemberInDevRelAsync()).Should().BeFalse();
        (await service.GetViewableProfileAsync(Handle)).Should().Be(ViewableProfile.None);
    }

    [Fact]
    public async Task The_group_is_matched_ignoring_case()
    {
        var service = CreateService(Viewer.DevRel, HiddenProfileProvider(), groups: ["devrel"]);

        (await service.IsCurrentMemberInDevRelAsync()).Should().BeTrue();
    }

    private static readonly ProfileSocialLink ShownLink = new("Blog", "https://example.dev");
    private static readonly ProfileSocialLink HiddenLink = new("Mastodon", "https://mastodon.example/@octocat", IsHiddenByMember: true);

    [Fact]
    public async Task DevRel_asking_for_everything_gets_the_hidden_links_marked()
    {
        var service = CreateService(Viewer.DevRel, LinksProvider(isProfileVisible: true));

        var viewable = await service.GetViewableProfileAsync(Handle, includeHiddenLinks: true);

        viewable.Profile!.Identity.SocialLinks.Should().Equal(ShownLink, HiddenLink);
        viewable.HasHiddenSocialLinks.Should().BeTrue();
    }

    [Fact]
    public async Task DevRel_viewing_what_the_public_sees_gets_no_hidden_links_but_knows_there_are_some()
    {
        var service = CreateService(Viewer.DevRel, LinksProvider(isProfileVisible: true));

        var viewable = await service.GetViewableProfileAsync(Handle);

        viewable.Profile!.Identity.SocialLinks.Should().Equal(ShownLink);
        viewable.HasHiddenSocialLinks.Should().BeTrue();
    }

    [Fact]
    public async Task DevRel_gets_the_hidden_links_of_a_hidden_profile_too()
    {
        var service = CreateService(Viewer.DevRel, LinksProvider(isProfileVisible: false));

        var viewable = await service.GetViewableProfileAsync(Handle, includeHiddenLinks: true);

        viewable.IsHiddenFromPublic.Should().BeTrue();
        viewable.Profile!.Identity.SocialLinks.Should().Equal(ShownLink, HiddenLink);
    }

    [Fact]
    public async Task Both_DevRel_views_on_one_request_load_the_profile_once()
    {
        var provider = LinksProvider(isProfileVisible: true);
        var service = CreateService(Viewer.DevRel, provider);

        await service.GetViewableProfileAsync(Handle, includeHiddenLinks: true);
        await service.GetViewableProfileAsync(Handle);

        provider.Verify(
            p => p.GetProfileAsync(Handle, It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData(Viewer.Anonymous)]
    [InlineData(Viewer.OtherMember)]
    [InlineData(Viewer.EarlyAccessMember)]
    [InlineData(Viewer.EarlyAccessOwner)]
    [InlineData(Viewer.DevRelOwner)]
    public async Task Nobody_else_gets_hidden_links_even_when_asking_for_them(Viewer viewer)
    {
        var provider = LinksProvider(isProfileVisible: true);
        var service = CreateService(viewer, provider);

        var viewable = await service.GetViewableProfileAsync(Handle, includeHiddenLinks: true);

        viewable.Profile!.Identity.SocialLinks.Should().Equal(ShownLink);
        viewable.HasHiddenSocialLinks.Should().BeFalse();
        provider.Verify(
            p => p.GetProfileAsync(It.IsAny<string>(), It.IsAny<bool>(), true, It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static ProfileAccessService CreateService(
        Viewer viewer,
        Mock<IProfileDataProvider> provider,
        ILogger<ProfileAccessService>? logger = null,
        string[]? groups = null)
    {
        var (handle, defaultGroups) = viewer switch
        {
            Viewer.Anonymous => ((string?)null, Array.Empty<string>()),
            Viewer.OtherMember => ("hubot", ["Community Members"]),
            Viewer.EarlyAccessMember => ("hubot", ["Community Members", ProfileAccessService.EarlyAccessGroupName]),
            Viewer.EarlyAccessOwner => (Handle, ["Community Members", ProfileAccessService.EarlyAccessGroupName]),
            Viewer.DevRel => (DevRelHandle, ["Community Members", ProfileAccessService.DevRelGroupName]),
            Viewer.DevRelOwner => (Handle, ["Community Members", ProfileAccessService.DevRelGroupName]),
            _ => throw new ArgumentOutOfRangeException(nameof(viewer)),
        };

        var member = handle != null ? new MemberIdentityUser { UserName = handle } : null;
        var memberManager = new Mock<IMemberManager>();
        memberManager.Setup(m => m.IsLoggedIn()).Returns(member != null);
        memberManager.Setup(m => m.GetCurrentMemberAsync()).ReturnsAsync(member);
        if (member != null)
        {
            memberManager.Setup(m => m.GetRolesAsync(member)).ReturnsAsync(groups ?? defaultGroups);
        }

        return new ProfileAccessService(memberManager.Object, provider.Object, logger ?? NullLogger<ProfileAccessService>.Instance);
    }

    // A provider holding one profile with a shown and a hidden link, as MemberProfileDataProvider returns them:
    // the hidden link only when hidden links are requested.
    private static Mock<IProfileDataProvider> LinksProvider(bool isProfileVisible)
    {
        var provider = new Mock<IProfileDataProvider>();
        provider
            .Setup(p => p.GetProfileAsync(Handle, It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, bool includeHidden, bool includeHiddenLinks, CancellationToken _) =>
                !isProfileVisible && !includeHidden
                    ? (CommunityProfile?)null
                    : Profile with
                    {
                        Identity = Profile.Identity with
                        {
                            SocialLinks = includeHiddenLinks ? [ShownLink, HiddenLink] : [ShownLink],
                        },
                    });
        return provider;
    }

    // A provider holding one hidden profile: returned only when hidden profiles are requested.
    private static Mock<IProfileDataProvider> HiddenProfileProvider()
    {
        var provider = new Mock<IProfileDataProvider>();
        provider
            .Setup(p => p.GetProfileAsync(Handle, false, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CommunityProfile?)null);
        provider
            .Setup(p => p.GetProfileAsync(Handle, true, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Profile);
        return provider;
    }
}

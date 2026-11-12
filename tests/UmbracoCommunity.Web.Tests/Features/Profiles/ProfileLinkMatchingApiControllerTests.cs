using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Umbraco.Cms.Core.Security;
using UmbracoCommunity.Web.Features.Profiles;
using UmbracoCommunity.Web.Features.Profiles.Controllers;
using UmbracoCommunity.Web.Features.Profiles.Data;
using UmbracoCommunity.Web.Features.Profiles.Data.Entities;
using UmbracoCommunity.Web.Features.Profiles.Matching;
using UmbracoCommunity.Web.Features.Profiles.Models;
using Xunit;

namespace UmbracoCommunity.Web.Tests.Features.Profiles;

public class ProfileLinkMatchingApiControllerTests : IDisposable
{
    private const string Handle = "octocat";

    private readonly MemberProfilesTestDatabase _database = new();
    private readonly MemberProfileStore _profileStore;
    private readonly ProfileMatchStore _matchStore;
    private readonly Mock<IProfileSyncClient> _syncClient = new();
    private readonly Guid _memberKey = Guid.NewGuid();

    public ProfileLinkMatchingApiControllerTests()
    {
        _profileStore = new MemberProfileStore(_database);
        _matchStore = new ProfileMatchStore(_database);
        _syncClient
            .Setup(c => c.FindProfileAsync(Handle, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlatformProfile("p1", [])
            {
                Identities = [new PlatformIdentity("mastodon", "octocat", "https://social.example/@octocat")],
                Feeds = [new PlatformFeed("https://octo.example/rss", "Octo's blog")],
            });
    }

    public void Dispose() => _database.Dispose();

    private async Task<MemberProfileEntity> CreateProfileAsync(bool completeOnboarding = true)
    {
        await using (var context = _database.CreateDbContext())
        {
            await context.Database.MigrateAsync();
        }

        var profile = await _profileStore.GetOrStartOnboardingAsync(_memberKey, Handle, "The Octocat");
        if (completeOnboarding)
        {
            await _profileStore.CompleteOnboardingAsync(_memberKey);
        }

        return profile;
    }

    private ProfileLinkMatchingApiController CreateController(bool signedIn = true, bool inEarlyAccess = true)
    {
        var member = signedIn ? new MemberIdentityUser { Key = _memberKey, UserName = Handle } : null;
        var memberManager = new Mock<IMemberManager>();
        memberManager.Setup(m => m.IsLoggedIn()).Returns(signedIn);
        memberManager.Setup(m => m.GetCurrentMemberAsync()).ReturnsAsync(member);
        if (member != null)
        {
            memberManager
                .Setup(m => m.GetRolesAsync(member))
                .ReturnsAsync(new List<string> { inEarlyAccess ? ProfileAccessService.EarlyAccessGroupName : "Community Members" });
        }

        var accessService = new ProfileAccessService(memberManager.Object, Mock.Of<IProfileDataProvider>(), NullLogger<ProfileAccessService>.Instance);
        var matching = new ProfileLinkMatchingService(
            _syncClient.Object,
            _matchStore,
            Mock.Of<IOptionsMonitor<ProfileMatchingOptions>>(o => o.CurrentValue == new ProfileMatchingOptions()),
            NullLogger<ProfileLinkMatchingService>.Instance);
        return new ProfileLinkMatchingApiController(memberManager.Object, _profileStore, accessService, matching);
    }

    [Fact]
    public async Task Suggestions_leave_out_the_members_own_links()
    {
        await CreateProfileAsync();
        await _profileStore.AddFeedAsync(_memberKey, "Mastodon", "https://social.example/@octocat");

        var result = await CreateController().Suggestions();

        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeAssignableTo<IReadOnlyList<LinkSuggestion>>()
            .Which.Should().Equal(new LinkSuggestion("https://octo.example/rss", null, "Octo's blog", LinkSuggestionKinds.Feed));
    }

    [Fact]
    public async Task Not_me_rejects_the_source_and_it_is_no_longer_suggested()
    {
        var profile = await CreateProfileAsync();
        var controller = CreateController();

        var result = await controller.NotMe(new RejectLinkRequest("https://octo.example/rss"));

        result.Should().BeOfType<OkResult>();
        (await _matchStore.GetRejectedSourcesAsync(Handle)).Should().Equal("https://octo.example/rss");
        (await _matchStore.GetForProfileAsync(profile.Id))["https://octo.example/rss"].DecidedBy.Should().Be(ProfileMatchDecider.Member);
        ((await controller.Suggestions()) as OkObjectResult)!.Value.Should().BeAssignableTo<IReadOnlyList<LinkSuggestion>>()
            .Which.Select(s => s.Url).Should().Equal("https://social.example/@octocat");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("http://octo.example")]
    [InlineData("not a link")]
    public async Task Not_me_with_an_invalid_address_is_rejected(string? url)
    {
        await CreateProfileAsync();

        var result = await CreateController().NotMe(new RejectLinkRequest(url));

        result.Should().BeOfType<BadRequestObjectResult>();
        (await _matchStore.GetRejectedSourcesAsync(Handle)).Should().BeEmpty();
    }

    [Fact]
    public async Task Not_me_for_the_members_own_github_profile_is_rejected()
    {
        await CreateProfileAsync();

        var result = await CreateController().NotMe(new RejectLinkRequest("https://github.com/octocat"));

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Members_outside_early_access_get_403_and_nothing_is_recorded()
    {
        await CreateProfileAsync();
        var controller = CreateController(inEarlyAccess: false);

        var suggestions = await controller.Suggestions();
        var notMe = await controller.NotMe(new RejectLinkRequest("https://octo.example/rss"));

        suggestions.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        notMe.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        (await _matchStore.GetRejectedSourcesAsync(Handle)).Should().BeEmpty();
        _syncClient.Verify(c => c.FindProfileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Signed_out_is_unauthorized()
    {
        await CreateProfileAsync();

        (await CreateController(signedIn: false).Suggestions()).Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task Without_a_finished_profile_the_request_is_rejected()
    {
        await CreateProfileAsync(completeOnboarding: false);

        (await CreateController().NotMe(new RejectLinkRequest("https://octo.example/rss"))).Should().BeOfType<BadRequestObjectResult>();
    }
}

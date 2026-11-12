using System.Net;
using FluentAssertions;
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
using UmbracoCommunity.Web.Features.Profiles.LinkPreviews;
using UmbracoCommunity.Web.Features.Profiles.Matching;
using UmbracoCommunity.Web.Features.Profiles.Models;
using UmbracoCommunity.Web.Tests.Features.Feeds.Calendar;
using Xunit;

namespace UmbracoCommunity.Web.Tests.Features.Profiles;

/// <summary>Links a member adds or changes are checked, and listed with their match status, only for early access members.</summary>
public class MemberFeedsMatchStatusTests : IDisposable
{
    private const string Handle = "octocat";
    private const string Mastodon = "https://social.example/@octocat";

    private readonly MemberProfilesTestDatabase _database = new();
    private readonly MemberProfileStore _profileStore;
    private readonly ProfileMatchStore _matchStore;
    private readonly Mock<IProfileSyncClient> _syncClient = new();
    private readonly Guid _memberKey = Guid.NewGuid();

    public MemberFeedsMatchStatusTests()
    {
        _profileStore = new MemberProfileStore(_database);
        _matchStore = new ProfileMatchStore(_database);
        _syncClient
            .Setup(c => c.VerifyLinksAsync(Handle, It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, IReadOnlyList<string> urls, CancellationToken _) =>
                urls.Select(u => new PlatformLinkCheck(u, PlatformLinkStatuses.Unknown, null)).ToList());
    }

    public void Dispose() => _database.Dispose();

    private async Task<MemberProfileEntity> CreateProfileAsync()
    {
        await using (var context = _database.CreateDbContext())
        {
            await context.Database.MigrateAsync();
        }

        return await _profileStore.GetOrStartOnboardingAsync(_memberKey, Handle, "The Octocat");
    }

    private MemberFeedsApiController CreateController(bool inEarlyAccess)
    {
        var member = new MemberIdentityUser { Key = _memberKey, UserName = Handle };
        var memberManager = new Mock<IMemberManager>();
        memberManager.Setup(m => m.IsLoggedIn()).Returns(true);
        memberManager.Setup(m => m.GetCurrentMemberAsync()).ReturnsAsync(member);
        memberManager
            .Setup(m => m.GetRolesAsync(member))
            .ReturnsAsync(new List<string> { inEarlyAccess ? ProfileAccessService.EarlyAccessGroupName : "Community Members" });

        var accessService = new ProfileAccessService(memberManager.Object, Mock.Of<IProfileDataProvider>(), NullLogger<ProfileAccessService>.Instance);
        var matching = new ProfileLinkMatchingService(
            _syncClient.Object,
            _matchStore,
            Mock.Of<IOptionsMonitor<ProfileMatchingOptions>>(o => o.CurrentValue == new ProfileMatchingOptions()),
            NullLogger<ProfileLinkMatchingService>.Instance);
        var linkPreviews = new LinkPreviewService(
            new LinkPreviewHttpClient(new HttpClient(StubHandler.Status(HttpStatusCode.NotFound))),
            NullLogger<LinkPreviewService>.Instance);

        return new MemberFeedsApiController(
            memberManager.Object, _profileStore, _syncClient.Object, linkPreviews, TestImageProxy.Create(), accessService, matching);
    }

    private static MemberFeedResponse Response(IActionResult result) =>
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeOfType<MemberFeedResponse>().Subject;

    [Fact]
    public async Task An_added_link_is_checked_and_returned_with_its_status_for_early_access_members()
    {
        var profile = await CreateProfileAsync();

        var added = Response(await CreateController(inEarlyAccess: true).Add(new AddFeedRequest("Mastodon", Mastodon), default));

        added.MatchStatus.Should().Be("Held");
        (await _matchStore.GetForProfileAsync(profile.Id))[Mastodon].State.Should().Be(ProfileMatchState.Held);
    }

    [Fact]
    public async Task An_added_link_is_not_checked_for_other_members()
    {
        var profile = await CreateProfileAsync();

        var added = Response(await CreateController(inEarlyAccess: false).Add(new AddFeedRequest("Mastodon", Mastodon), default));

        added.MatchStatus.Should().BeNull();
        (await _matchStore.GetForProfileAsync(profile.Id)).Should().BeEmpty();
        _syncClient.Verify(c => c.VerifyLinksAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_changed_address_is_checked_but_an_unchanged_one_is_not_checked_again()
    {
        var profile = await CreateProfileAsync();
        var feed = await _profileStore.AddFeedAsync(_memberKey, "Mastodon", Mastodon);
        var controller = CreateController(inEarlyAccess: true);

        var renamed = Response(await controller.Update(feed!.Id, new UpdateFeedRequest("My Mastodon", Mastodon), default));
        _syncClient.Verify(c => c.VerifyLinksAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()), Times.Never);
        renamed.MatchStatus.Should().BeNull();

        var moved = Response(await controller.Update(feed.Id, new UpdateFeedRequest("Mastodon", "https://social.example/@octo"), default));
        moved.MatchStatus.Should().Be("Held");
        (await _matchStore.GetForProfileAsync(profile.Id)).Keys.Should().Equal("https://social.example/@octo");
    }

    [Fact]
    public async Task The_list_carries_match_statuses_only_for_early_access_members()
    {
        var profile = await CreateProfileAsync();
        await _profileStore.AddFeedAsync(_memberKey, "Mastodon", Mastodon);
        await _profileStore.AddFeedAsync(_memberKey, "Blog", "https://octo.example");
        await _matchStore.RecordMemberRejectionAsync(profile.Id, Mastodon, []);

        var early = (await CreateController(inEarlyAccess: true).List(default)).Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeAssignableTo<IEnumerable<MemberFeedResponse>>().Subject.ToList();
        var others = (await CreateController(inEarlyAccess: false).List(default)).Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeAssignableTo<IEnumerable<MemberFeedResponse>>().Subject.ToList();

        early.Select(f => (f.Url, f.MatchStatus)).Should().BeEquivalentTo(new[] { (Mastodon, (string?)"Rejected"), ("https://octo.example", (string?)null) });
        others.Should().OnlyContain(f => f.MatchStatus == null);
    }
}

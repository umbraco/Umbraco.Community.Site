using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using UmbracoCommunity.Web.Features.Profiles;
using UmbracoCommunity.Web.Features.Profiles.Data;
using UmbracoCommunity.Web.Features.Profiles.Data.Entities;
using UmbracoCommunity.Web.Features.Profiles.Matching;
using UmbracoCommunity.Web.Features.Profiles.Models;
using Xunit;

namespace UmbracoCommunity.Web.Tests.Features.Profiles;

public class ProfileLinkMatchingServiceTests : IDisposable
{
    private const string Handle = "octocat";
    private const string Mastodon = "https://social.example/@octocat";
    private const string Blog = "https://octo.example/blog";
    private const string Elsewhere = "https://other.example/someone";

    private readonly MemberProfilesTestDatabase _database = new();
    private readonly ProfileMatchStore _store;
    private readonly Mock<IProfileSyncClient> _syncClient = new();
    private readonly ProfileLinkMatchingService _service;
    private int _profileId;

    public ProfileLinkMatchingServiceTests()
    {
        _store = new ProfileMatchStore(_database);
        _service = new ProfileLinkMatchingService(
            _syncClient.Object,
            _store,
            Mock.Of<IOptionsMonitor<ProfileMatchingOptions>>(o => o.CurrentValue == new ProfileMatchingOptions()),
            NullLogger<ProfileLinkMatchingService>.Instance);
    }

    public void Dispose() => _database.Dispose();

    private async Task<int> ProfileAsync()
    {
        if (_profileId != 0)
        {
            return _profileId;
        }

        await using (var context = _database.CreateDbContext())
        {
            await context.Database.MigrateAsync();
        }

        var profile = await new MemberProfileStore(_database).GetOrStartOnboardingAsync(Guid.NewGuid(), Handle, "The Octocat");
        return _profileId = profile.Id;
    }

    private void PlatformAnswers(params PlatformLinkCheck[] checks) =>
        _syncClient
            .Setup(c => c.VerifyLinksAsync(Handle, It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(checks);

    private async Task<ProfileMatchRecord?> MatchAsync(string url) =>
        (await _store.GetForProfileAsync(await ProfileAsync())).GetValueOrDefault(url);

    [Theory]
    [InlineData("https://github.com/octocat")]
    [InlineData("https://github.com/OctoCat/")]
    [InlineData("https://www.github.com/octocat")]
    public async Task The_members_own_github_profile_is_confirmed_without_asking_the_platform(string url)
    {
        var id = await ProfileAsync();

        await _service.CheckLinksAsync(id, Handle, [url]);

        var match = await MatchAsync(url);
        match!.State.Should().Be(ProfileMatchState.Confirmed);
        match.Confidence.Should().Be(1.0);
        match.Signals.Should().ContainSingle().Which.Name.Should().Be("GitHub handle");
        _syncClient.Verify(c => c.VerifyLinksAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Someone_elses_github_profile_is_not_trusted_by_the_handle()
    {
        var id = await ProfileAsync();
        PlatformAnswers(new PlatformLinkCheck("https://github.com/someone", PlatformLinkStatuses.Unknown, "github"));

        await _service.CheckLinksAsync(id, Handle, ["https://github.com/someone"]);

        (await MatchAsync("https://github.com/someone"))!.State.Should().Be(ProfileMatchState.Held);
    }

    [Fact]
    public async Task A_link_the_platform_already_ties_to_the_member_is_confirmed()
    {
        var id = await ProfileAsync();
        PlatformAnswers(new PlatformLinkCheck(Mastodon, PlatformLinkStatuses.Confirmed, "mastodon"));

        await _service.CheckLinksAsync(id, Handle, [Mastodon]);

        var match = await MatchAsync(Mastodon);
        match!.State.Should().Be(ProfileMatchState.Confirmed);
        match.Signals.Should().ContainSingle().Which.Detail.Should().Be("Platform already links this account");
        _syncClient.Verify(c => c.VerifyLinksAsync(Handle, It.Is<IReadOnlyList<string>>(l => l.SequenceEqual(new[] { Mastodon })), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task A_link_tied_to_someone_else_is_held_without_saying_who()
    {
        var id = await ProfileAsync();
        PlatformAnswers(new PlatformLinkCheck(Mastodon, PlatformLinkStatuses.Conflict, "mastodon"));

        await _service.CheckLinksAsync(id, Handle, [Mastodon]);

        var match = await MatchAsync(Mastodon);
        match!.State.Should().Be(ProfileMatchState.Held);
        match.Confidence.Should().Be(0.1);
        match.Signals.Should().Equal(new MatchSignal("Content platform", "Points to an account linked to someone else"));
    }

    [Fact]
    public async Task A_link_the_platform_knows_nothing_about_is_held()
    {
        var id = await ProfileAsync();
        PlatformAnswers(new PlatformLinkCheck(Blog, PlatformLinkStatuses.Unknown, null));

        await _service.CheckLinksAsync(id, Handle, [Blog]);

        var match = await MatchAsync(Blog);
        match!.State.Should().Be(ProfileMatchState.Held);
        match.Confidence.Should().Be(0.3);
        match.Signals.Should().ContainSingle().Which.Detail.Should().Be("Nothing known about this link");
    }

    [Fact]
    public async Task A_link_the_platform_cannot_check_is_not_recorded()
    {
        var id = await ProfileAsync();
        PlatformAnswers(new PlatformLinkCheck(Elsewhere, PlatformLinkStatuses.Unsupported, null));

        await _service.CheckLinksAsync(id, Handle, [Elsewhere]);

        (await _store.GetForProfileAsync(id)).Should().BeEmpty();
    }

    [Fact]
    public async Task The_platform_answer_is_matched_to_the_link_ignoring_a_trailing_slash_and_host_case()
    {
        var id = await ProfileAsync();
        PlatformAnswers(new PlatformLinkCheck("https://Social.Example/@octocat/", PlatformLinkStatuses.Confirmed, "mastodon"));

        await _service.CheckLinksAsync(id, Handle, [Mastodon]);

        (await MatchAsync(Mastodon))!.State.Should().Be(ProfileMatchState.Confirmed);
    }

    [Fact]
    public async Task A_platform_failure_records_nothing_and_does_not_throw()
    {
        var id = await ProfileAsync();
        _syncClient
            .Setup(c => c.VerifyLinksAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("simulated"));

        var act = () => _service.CheckLinksAsync(id, Handle, [Mastodon, "https://github.com/octocat"]);

        await act.Should().NotThrowAsync();
        (await MatchAsync(Mastodon)).Should().BeNull();
    }

    [Fact]
    public async Task Links_that_are_not_https_are_not_checked()
    {
        var id = await ProfileAsync();

        await _service.CheckLinksAsync(id, Handle, ["http://octo.example", "not a link"]);

        (await _store.GetForProfileAsync(id)).Should().BeEmpty();
        _syncClient.Verify(c => c.VerifyLinksAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_decision_a_person_made_is_not_replaced_by_a_new_check(bool byMember)
    {
        var id = await ProfileAsync();
        PlatformAnswers(new PlatformLinkCheck(Mastodon, PlatformLinkStatuses.Unknown, null));
        await _service.CheckLinksAsync(id, Handle, [Mastodon]);
        if (byMember)
        {
            await _service.RejectSourceAsync(id, Handle, Mastodon);
        }
        else
        {
            await _store.DecideManuallyAsync((await MatchAsync(Mastodon))!.Id, ProfileMatchState.Rejected, null, "A Reviewer");
        }

        PlatformAnswers(new PlatformLinkCheck(Mastodon, PlatformLinkStatuses.Confirmed, null));
        await _service.CheckLinksAsync(id, Handle, [Mastodon]);

        var match = await MatchAsync(Mastodon);
        match!.State.Should().Be(ProfileMatchState.Rejected);
        match.DecidedBy.Should().Be(byMember ? ProfileMatchDecider.Member : ProfileMatchDecider.Manual);
    }

    [Fact]
    public async Task Link_statuses_are_read_for_the_given_links()
    {
        var id = await ProfileAsync();
        PlatformAnswers(
            new PlatformLinkCheck(Mastodon, PlatformLinkStatuses.Confirmed, null),
            new PlatformLinkCheck(Blog, PlatformLinkStatuses.Unknown, null));
        await _service.CheckLinksAsync(id, Handle, [Mastodon, Blog]);
        await _service.RejectSourceAsync(id, Handle, Elsewhere);

        var statuses = await _service.GetLinkStatusesAsync(id, [Mastodon, Blog + "/", Elsewhere, "https://unchecked.example"]);

        statuses.Should().BeEquivalentTo(new Dictionary<string, string>
        {
            [Mastodon] = "Confirmed",
            [Blog + "/"] = "Held",
            [Elsewhere] = "Rejected",
        });
    }

    [Fact]
    public async Task Rejecting_a_source_records_it_for_review()
    {
        var id = await ProfileAsync();

        (await _service.RejectSourceAsync(id, Handle, Elsewhere)).Should().BeTrue();

        var listed = (await _store.ListAsync(ProfileMatchState.Rejected, 0, 10)).Items.Should().ContainSingle().Subject;
        listed.SourceUrl.Should().Be(Elsewhere);
        listed.DecidedBy.Should().Be(ProfileMatchDecider.Member);
        listed.Signals.Should().ContainSingle().Which.Detail.Should().Be("Member said this is not theirs");
    }

    [Theory]
    [InlineData("http://other.example/someone")]
    [InlineData("not a link")]
    [InlineData("https://github.com/octocat")]
    public async Task A_source_that_is_invalid_or_the_members_own_github_cannot_be_rejected(string url)
    {
        var id = await ProfileAsync();

        (await _service.RejectSourceAsync(id, Handle, url)).Should().BeFalse();
        (await _store.GetForProfileAsync(id)).Should().BeEmpty();
    }

    [Fact]
    public async Task Suggestions_leave_out_links_the_member_has_or_rejected()
    {
        var id = await ProfileAsync();
        _syncClient
            .Setup(c => c.FindProfileAsync(Handle, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlatformProfile("p1", [])
            {
                Identities =
                [
                    new PlatformIdentity("mastodon", "octocat", Mastodon),
                    new PlatformIdentity("bluesky", "octocat.example", "https://bsky.example/profile/octocat"),
                    new PlatformIdentity("other", "someone", Elsewhere),
                    new PlatformIdentity("insecure", "x", "http://insecure.example/x"),
                ],
                Feeds =
                [
                    new PlatformFeed("https://octo.example/rss", "Octo's blog"),
                    new PlatformFeed("https://OCTO.example/rss/", "The same feed again"),
                ],
            });
        await _service.RejectSourceAsync(id, Handle, Elsewhere);

        var suggestions = await _service.GetSuggestionsAsync(Handle, ["https://Social.Example/@octocat/"]);

        suggestions.Should().Equal(
            new LinkSuggestion("https://bsky.example/profile/octocat", "bluesky", "octocat.example", LinkSuggestionKinds.Account),
            new LinkSuggestion("https://octo.example/rss", null, "Octo's blog", LinkSuggestionKinds.Feed));
    }

    [Fact]
    public async Task There_are_no_suggestions_when_the_platform_fails_or_has_no_profile()
    {
        await ProfileAsync();
        _syncClient.Setup(c => c.FindProfileAsync(Handle, It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("simulated"));
        (await _service.GetSuggestionsAsync(Handle, [])).Should().BeEmpty();

        _syncClient.Setup(c => c.FindProfileAsync(Handle, It.IsAny<CancellationToken>())).ReturnsAsync((PlatformProfile?)null);
        (await _service.GetSuggestionsAsync(Handle, [])).Should().BeEmpty();
    }

    [Theory]
    [InlineData("https://octo.example", "https://octo.example/posts/one", true)]
    [InlineData("https://octo.example/", "https://www.octo.example/posts/one", true)]
    [InlineData("https://www.octo.example/blog", "https://octo.example/blog/one", true)]
    [InlineData("https://octo.example/blog/", "https://octo.example/blog", true)]
    [InlineData("https://octo.example/Blog", "https://octo.example/blog/one", true)]
    [InlineData("https://octo.example/blog", "https://octo.example/blogroll/one", false)]
    [InlineData("https://octo.example/blog", "https://octo.example/other/one", false)]
    [InlineData("https://octo.example/blog/rss", "https://octo.example/posts/one", true)]
    [InlineData("https://octo.example/feed", "https://octo.example/2026/10/a-post", true)]
    [InlineData("https://octo.example/atom", "https://octo.example/a-post", true)]
    [InlineData("https://octo.example/index.xml", "https://octo.example/a-post", true)]
    [InlineData("https://octo.example/posts.rss", "https://octo.example/a-post", true)]
    [InlineData("https://octo.example/posts.atom", "https://octo.example/a-post", true)]
    [InlineData("https://octo.example/rss", "https://other.example/a-post", false)]
    [InlineData("https://social.example/@octocat", "https://social.example/@octocat/123", true)]
    [InlineData("https://social.example/@octocat", "https://social.example/@someone/123", false)]
    [InlineData("https://octo.example", "https://sub.octo.example/a-post", false)]
    [InlineData("not a link", "https://octo.example/a-post", false)]
    [InlineData("https://octo.example", "not a link", false)]
    public void A_source_covers_activity_on_its_host_under_its_path(string source, string activity, bool expected)
    {
        ProfileLinkMatchingService.SourceCovers(source, activity).Should().Be(expected);
    }
}

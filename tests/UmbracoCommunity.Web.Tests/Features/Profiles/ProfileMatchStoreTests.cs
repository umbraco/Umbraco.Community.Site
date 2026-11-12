using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using UmbracoCommunity.Web.Features.Profiles.Data;
using UmbracoCommunity.Web.Features.Profiles.Data.Entities;
using UmbracoCommunity.Web.Features.Profiles.Matching;
using Xunit;

namespace UmbracoCommunity.Web.Tests.Features.Profiles;

public class ProfileMatchStoreTests : IDisposable
{
    private const string Source = "https://example.org/someone";

    private readonly MemberProfilesTestDatabase _database = new();
    private readonly ProfileMatchStore _store;
    private int _profileId;

    public ProfileMatchStoreTests()
    {
        _store = new ProfileMatchStore(_database);
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

        var profile = await new MemberProfileStore(_database)
            .GetOrStartOnboardingAsync(Guid.NewGuid(), "octocat", "The Octocat");
        return _profileId = profile.Id;
    }

    private static readonly IReadOnlyList<MatchSignal> Signals = [new("Link", "Linked from their blog")];

    [Fact]
    public async Task A_held_match_is_listed_with_its_evidence()
    {
        var id = await ProfileAsync();
        await _store.RecordAutomaticAsync(id, Source, ProfileMatchState.Held, 0.4, Signals);

        var page = await _store.ListAsync(ProfileMatchState.Held, 0, 10);

        page.Total.Should().Be(1);
        var match = page.Items.Single();
        match.GitHubHandle.Should().Be("octocat");
        match.SourceUrl.Should().Be(Source);
        match.Confidence.Should().Be(0.4);
        match.Signals.Should().ContainSingle().Which.Should().Be(new MatchSignal("Link", "Linked from their blog"));
        match.DecidedBy.Should().Be(ProfileMatchDecider.Automatic);
    }

    [Fact]
    public async Task An_automatic_run_replaces_an_earlier_automatic_decision()
    {
        var id = await ProfileAsync();
        await _store.RecordAutomaticAsync(id, Source, ProfileMatchState.Held, 0.4, Signals);

        await _store.RecordAutomaticAsync(id, Source, ProfileMatchState.Confirmed, 0.9, []);

        var page = await _store.ListAsync(ProfileMatchState.Confirmed, 0, 10);
        page.Items.Should().ContainSingle().Which.Confidence.Should().Be(0.9);
        (await _store.ListAsync(ProfileMatchState.Held, 0, 10)).Total.Should().Be(0);
    }

    [Theory]
    [InlineData(ProfileMatchState.Confirmed)]
    [InlineData(ProfileMatchState.Rejected)]
    public async Task A_manual_decision_sticks_when_the_match_is_run_again(ProfileMatchState decision)
    {
        var id = await ProfileAsync();
        var held = await _store.RecordAutomaticAsync(id, Source, ProfileMatchState.Held, 0.4, Signals);
        var userKey = Guid.NewGuid();

        var decided = await _store.DecideManuallyAsync(held!.Id, decision, userKey, "A Reviewer");
        var rerun = await _store.RecordAutomaticAsync(id, Source, ProfileMatchState.Held, 0.2, []);

        decided!.State.Should().Be(decision);
        decided.DecidedBy.Should().Be(ProfileMatchDecider.Manual);
        decided.DecidedByUserName.Should().Be("A Reviewer");
        rerun!.State.Should().Be(decision);
        rerun.Confidence.Should().Be(0.4);
        rerun.Signals.Should().HaveCount(1);
        rerun.DecidedBy.Should().Be(ProfileMatchDecider.Manual);
    }

    [Fact]
    public async Task A_manual_decision_cannot_be_to_hold()
    {
        var id = await ProfileAsync();
        var held = await _store.RecordAutomaticAsync(id, Source, ProfileMatchState.Held, 0.4, Signals);

        var act = () => _store.DecideManuallyAsync(held!.Id, ProfileMatchState.Held, null, null);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Deciding_an_unknown_match_returns_null()
    {
        await ProfileAsync();

        (await _store.DecideManuallyAsync(999, ProfileMatchState.Rejected, null, null)).Should().BeNull();
    }

    [Fact]
    public async Task Recording_for_an_unknown_profile_returns_null()
    {
        await ProfileAsync();

        (await _store.RecordAutomaticAsync(999, Source, ProfileMatchState.Held, 0.5, [])).Should().BeNull();
    }

    [Fact]
    public async Task A_source_that_is_too_long_is_refused()
    {
        var id = await ProfileAsync();
        var tooLong = "https://example.org/" + new string('a', ProfileMatchEntity.MaxSourceUrlLength);

        var act = () => _store.RecordAutomaticAsync(id, tooLong, ProfileMatchState.Held, 0.5, []);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task A_member_rejection_is_recorded_when_there_was_no_match_and_listed_as_rejected()
    {
        var id = await ProfileAsync();

        var rejected = await _store.RecordMemberRejectionAsync(id, Source, [new MatchSignal("Member", "Not theirs")]);

        rejected!.State.Should().Be(ProfileMatchState.Rejected);
        rejected.DecidedBy.Should().Be(ProfileMatchDecider.Member);
        var listed = (await _store.ListAsync(ProfileMatchState.Rejected, 0, 10)).Items.Should().ContainSingle().Subject;
        listed.SourceUrl.Should().Be(Source);
        listed.DecidedBy.Should().Be(ProfileMatchDecider.Member);
        listed.Signals.Should().Equal(new MatchSignal("Member", "Not theirs"));
    }

    [Fact]
    public async Task A_member_rejection_keeps_the_evidence_already_stored()
    {
        var id = await ProfileAsync();
        await _store.RecordAutomaticAsync(id, Source, ProfileMatchState.Confirmed, 0.9, Signals);

        var rejected = await _store.RecordMemberRejectionAsync(id, Source, [new MatchSignal("Member", "Not theirs")]);

        rejected!.State.Should().Be(ProfileMatchState.Rejected);
        rejected.Confidence.Should().Be(0.9);
        rejected.Signals.Should().Equal(Signals[0], new MatchSignal("Member", "Not theirs"));
        (await _store.ListAsync(ProfileMatchState.Confirmed, 0, 10)).Total.Should().Be(0);
    }

    [Fact]
    public async Task A_member_decision_sticks_when_the_match_is_run_again()
    {
        var id = await ProfileAsync();
        await _store.RecordMemberRejectionAsync(id, Source, []);

        var rerun = await _store.RecordAutomaticAsync(id, Source, ProfileMatchState.Confirmed, 1, Signals);

        rerun!.State.Should().Be(ProfileMatchState.Rejected);
        rerun.DecidedBy.Should().Be(ProfileMatchDecider.Member);
    }

    [Fact]
    public async Task A_reviewer_can_still_overrule_a_member_decision()
    {
        var id = await ProfileAsync();
        var rejected = await _store.RecordMemberRejectionAsync(id, Source, []);

        var decided = await _store.DecideManuallyAsync(rejected!.Id, ProfileMatchState.Confirmed, Guid.NewGuid(), "A Reviewer");

        decided!.State.Should().Be(ProfileMatchState.Confirmed);
        decided.DecidedBy.Should().Be(ProfileMatchDecider.Manual);
    }

    [Fact]
    public async Task A_member_rejection_for_an_unknown_profile_returns_null()
    {
        await ProfileAsync();

        (await _store.RecordMemberRejectionAsync(999, Source, [])).Should().BeNull();
    }

    [Fact]
    public async Task The_rejected_sources_of_a_handle_are_found_ignoring_case()
    {
        var id = await ProfileAsync();
        await _store.RecordMemberRejectionAsync(id, Source, []);
        var held = await _store.RecordAutomaticAsync(id, "https://example.org/held", ProfileMatchState.Held, 0.3, []);
        var byReviewer = await _store.RecordAutomaticAsync(id, "https://example.org/reviewed", ProfileMatchState.Held, 0.3, []);
        await _store.DecideManuallyAsync(byReviewer!.Id, ProfileMatchState.Rejected, null, "A Reviewer");

        var sources = await _store.GetRejectedSourcesAsync("OctoCat");

        sources.Should().BeEquivalentTo(Source, "https://example.org/reviewed");
        held.Should().NotBeNull();
        (await _store.GetRejectedSourcesAsync("someone-else")).Should().BeEmpty();
    }

    [Fact]
    public async Task The_matches_of_a_profile_are_keyed_by_source()
    {
        var id = await ProfileAsync();
        await _store.RecordAutomaticAsync(id, Source, ProfileMatchState.Held, 0.3, []);
        await _store.RecordAutomaticAsync(id, "https://example.org/other", ProfileMatchState.Confirmed, 1, []);

        var matches = await _store.GetForProfileAsync(id);

        matches.Keys.Should().BeEquivalentTo(Source, "https://example.org/other");
        matches[Source].State.Should().Be(ProfileMatchState.Held);
        (await _store.GetForProfileAsync(999)).Should().BeEmpty();
    }
}

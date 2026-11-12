using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Security;
using UmbracoCommunity.ProfileMatchReview.Api;
using UmbracoCommunity.ProfileMatchReview.Api.Models;
using UmbracoCommunity.Web.Features.Profiles.Data;
using UmbracoCommunity.Web.Features.Profiles.Data.Entities;
using UmbracoCommunity.Web.Features.Profiles.Matching;
using Xunit;

namespace UmbracoCommunity.ProfileMatchReview.Tests;

public class ProfileMatchReviewApiControllerTests : IDisposable
{
    private const string Source = "https://example.org/someone";

    private readonly ProfileMatchReviewTestDatabase _database = new();
    private readonly ProfileMatchStore _store;
    private int _profileId;

    public ProfileMatchReviewApiControllerTests()
    {
        _store = new ProfileMatchStore(_database);
    }

    public void Dispose() => _database.Dispose();

    private static ProfileMatchReviewApiController ControllerFor(
        ProfileMatchStore store,
        string? groupAlias,
        Guid? userKey = null,
        string userName = "Reviewer")
    {
        var user = new Mock<IUser>();
        user.SetupGet(u => u.Key).Returns(userKey ?? Guid.NewGuid());
        user.SetupGet(u => u.Name).Returns(userName);
        user.SetupGet(u => u.Groups).Returns(
            groupAlias == null
                ? []
                : [Mock.Of<IReadOnlyUserGroup>(g => g.Alias == groupAlias)]);

        var security = new Mock<IBackOfficeSecurity>();
        security.SetupGet(s => s.CurrentUser).Returns(user.Object);
        var accessor = new Mock<IBackOfficeSecurityAccessor>();
        accessor.SetupGet(a => a.BackOfficeSecurity).Returns(security.Object);

        return new ProfileMatchReviewApiController(store, accessor.Object);
    }

    private async Task<int> HeldMatchAsync()
    {
        await using (var context = _database.CreateDbContext())
        {
            await context.Database.MigrateAsync();
        }

        var profile = await new MemberProfileStore(_database)
            .GetOrStartOnboardingAsync(Guid.NewGuid(), "octocat", "The Octocat");
        _profileId = profile.Id;
        var match = await _store.RecordAutomaticAsync(
            profile.Id, Source, ProfileMatchState.Held, 0.4, [new MatchSignal("Link", "Linked from their blog")]);
        return match!.Id;
    }

    [Theory]
    [InlineData(null)]
    [InlineData("editor")]
    public async Task A_user_outside_the_reviewer_group_gets_403_and_cannot_decide(string? group)
    {
        var id = await HeldMatchAsync();
        var controller = ControllerFor(_store, group);

        controller.Access().Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(403);
        (await controller.List()).Result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(403);
        (await controller.Link(id)).Result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(403);
        (await controller.Reject(id)).Result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(403);

        (await _store.ListAsync(ProfileMatchState.Held, 0, 10)).Total.Should().Be(1);
    }

    [Theory]
    [InlineData(ProfileMatchReviewApiConstants.ReviewerGroupAlias)]
    [InlineData(Constants.Security.AdminGroupAlias)]
    public async Task Reviewers_and_administrators_see_the_held_match_with_its_evidence(string group)
    {
        await HeldMatchAsync();
        var controller = ControllerFor(_store, group);

        controller.Access().Should().BeOfType<NoContentResult>();
        var response = (await controller.List()).Value!;

        response.Total.Should().Be(1);
        var match = response.Items.Single();
        match.Handle.Should().Be("octocat");
        match.SourceUrl.Should().Be(Source);
        match.Signals.Should().ContainSingle().Which.Name.Should().Be("Link");
    }

    [Theory]
    [InlineData(true, ProfileMatchState.Confirmed)]
    [InlineData(false, ProfileMatchState.Rejected)]
    public async Task Link_and_reject_record_who_decided_and_stick(bool link, ProfileMatchState expected)
    {
        var id = await HeldMatchAsync();
        var userKey = Guid.NewGuid();
        var controller = ControllerFor(_store, ProfileMatchReviewApiConstants.ReviewerGroupAlias, userKey, "Ada");

        var result = link ? await controller.Link(id) : await controller.Reject(id);

        var decided = result.Value!;
        decided.State.Should().Be(expected.ToString());
        decided.DecidedBy.Should().Be(ProfileMatchDecider.Manual.ToString());
        decided.DecidedByUserName.Should().Be("Ada");

        // A later automatic run leaves the manual decision alone.
        var profileId = _profileId;
        await _store.RecordAutomaticAsync(profileId, Source, ProfileMatchState.Held, 0.1, []);
        (await _store.ListAsync(expected, 0, 10)).Total.Should().Be(1);
        (await _store.ListAsync(ProfileMatchState.Held, 0, 10)).Total.Should().Be(0);
    }

    [Fact]
    public async Task Deciding_an_unknown_match_is_a_404()
    {
        await HeldMatchAsync();
        var controller = ControllerFor(_store, ProfileMatchReviewApiConstants.ReviewerGroupAlias);

        (await controller.Link(9999)).Result.Should().BeOfType<NotFoundResult>();
        (await controller.Reject(9999)).Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task A_member_saying_a_source_is_not_theirs_shows_as_rejected_by_the_member_and_can_be_overruled()
    {
        await HeldMatchAsync();
        await _store.RecordMemberRejectionAsync(_profileId, Source, [new MatchSignal("Member", "Member said this is not theirs")]);
        var controller = ControllerFor(_store, ProfileMatchReviewApiConstants.ReviewerGroupAlias, userName: "Ada");

        var rejected = (await controller.List(ProfileMatchState.Rejected)).Value!.Items.Should().ContainSingle().Subject;

        rejected.DecidedBy.Should().Be(ProfileMatchDecider.Member.ToString());
        rejected.DecidedByUserName.Should().BeNull();
        rejected.Signals.Select(s => s.Name).Should().Equal("Link", "Member");

        var linked = (await controller.Link(rejected.Id)).Value!;
        linked.State.Should().Be(ProfileMatchState.Confirmed.ToString());
        linked.DecidedBy.Should().Be(ProfileMatchDecider.Manual.ToString());
    }
}

using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Security;
using UmbracoCommunity.ProfileMatchReview.Api;
using UmbracoCommunity.ProfileMatchReview.Api.Models;
using UmbracoCommunity.Web.Features.Profiles;
using UmbracoCommunity.Web.Features.Profiles.Data;
using UmbracoCommunity.Web.Features.Profiles.Data.Entities;
using UmbracoCommunity.Web.Features.Profiles.Models;
using Xunit;

namespace UmbracoCommunity.ProfileMatchReview.Tests;

public class ProfileActivityReviewApiControllerTests : IDisposable
{
    private readonly ProfileMatchReviewTestDatabase _database = new();
    private readonly ProfileActivityEntryService _service;
    private int _entryId;

    public ProfileActivityReviewApiControllerTests()
    {
        _service = new ProfileActivityEntryService(
            new MemberActivityEntryStore(_database),
            Mock.Of<IProfileSyncClient>(),
            new AppCaches(new ObjectCacheAppCache(), NoAppCache.Instance, new IsolatedCaches(_ => NoAppCache.Instance)),
            NullLogger<ProfileActivityEntryService>.Instance);
    }

    public void Dispose() => _database.Dispose();

    private ProfileActivityReviewApiController ControllerFor(string? groupAlias, string userName = "Reviewer")
    {
        var user = new Mock<IUser>();
        user.SetupGet(u => u.Key).Returns(Guid.NewGuid());
        user.SetupGet(u => u.Name).Returns(userName);
        user.SetupGet(u => u.Groups).Returns(
            groupAlias == null ? [] : [Mock.Of<IReadOnlyUserGroup>(g => g.Alias == groupAlias)]);

        var security = new Mock<IBackOfficeSecurity>();
        security.SetupGet(s => s.CurrentUser).Returns(user.Object);
        var accessor = new Mock<IBackOfficeSecurityAccessor>();
        accessor.SetupGet(a => a.BackOfficeSecurity).Returns(security.Object);
        return new ProfileActivityReviewApiController(_service, accessor.Object);
    }

    private async Task AddPendingEntryAsync()
    {
        await using (var context = _database.CreateDbContext())
        {
            await context.Database.MigrateAsync();
        }

        var profile = await new MemberProfileStore(_database).GetOrStartOnboardingAsync(Guid.NewGuid(), "octocat", "The Octocat");
        var entry = await _service.AddAsync(
            profile.Id, new AddActivityEntryRequest("blog", "My post", "https://example.dev/a", new DateOnly(2026, 10, 1), null));
        _entryId = entry!.Id;
    }

    [Theory]
    [InlineData(null)]
    [InlineData("editor")]
    public async Task A_user_outside_the_reviewer_group_gets_403_and_cannot_decide(string? group)
    {
        await AddPendingEntryAsync();
        var controller = ControllerFor(group);

        (await controller.List()).Result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(403);
        (await controller.Verify(_entryId)).Result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(403);
        (await controller.Reject(_entryId, new RejectActivityEntryRequest("No."))).Result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(403);

        (await _service.ListAsync(ActivityEntryState.Pending, 0, 10)).Total.Should().Be(1);
    }

    [Fact]
    public async Task A_reviewer_sees_the_waiting_entry_and_verifies_it()
    {
        await AddPendingEntryAsync();
        var controller = ControllerFor(ProfileMatchReviewApiConstants.ReviewerGroupAlias, "Rita");

        var list = (await controller.List()).Value!;
        list.Total.Should().Be(1);
        list.Items.Single().Handle.Should().Be("octocat");

        var verified = (await controller.Verify(_entryId)).Value!;
        verified.State.Should().Be("Verified");
        verified.DecidedByUserName.Should().Be("Rita");
        (await controller.List()).Value!.Total.Should().Be(0);
    }

    [Fact]
    public async Task Rejecting_needs_a_reason_and_the_reason_sticks()
    {
        await AddPendingEntryAsync();
        var controller = ControllerFor(ProfileMatchReviewApiConstants.ReviewerGroupAlias);

        (await controller.Reject(_entryId, new RejectActivityEntryRequest("  "))).Result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(400);
        (await _service.ListAsync(ActivityEntryState.Pending, 0, 10)).Total.Should().Be(1);

        var rejected = (await controller.Reject(_entryId, new RejectActivityEntryRequest("Not your page."))).Value!;

        rejected.State.Should().Be("Rejected");
        rejected.RejectionReason.Should().Be("Not your page.");
        (await controller.Reject(_entryId, new RejectActivityEntryRequest("Again."))).Result.Should().BeOfType<NotFoundResult>();

        var verifiedAfterAll = (await controller.Verify(_entryId)).Value!;
        verifiedAfterAll.State.Should().Be("Verified");
        verifiedAfterAll.RejectionReason.Should().BeNull();

        var rejectedAgain = (await controller.Reject(_entryId, new RejectActivityEntryRequest("Changed my mind."))).Value!;
        rejectedAgain.State.Should().Be("Rejected");
        rejectedAgain.RejectionReason.Should().Be("Changed my mind.");
    }
}

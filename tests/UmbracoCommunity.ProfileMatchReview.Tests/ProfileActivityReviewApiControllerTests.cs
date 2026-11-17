using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.IO;
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
            new ProfileActivityImageStore(MediaFileSystem().Object, NullLogger<ProfileActivityImageStore>.Instance),
            new AppCaches(new ObjectCacheAppCache(), NoAppCache.Instance, new IsolatedCaches(_ => NoAppCache.Instance)),
            NullLogger<ProfileActivityEntryService>.Instance);
    }

    public void Dispose() => _database.Dispose();

    /// <summary>A media file system that serves its files under <c>/media/</c>.</summary>
    private static Mock<IFileSystem> MediaFileSystem()
    {
        var fileSystem = new Mock<IFileSystem>();
        fileSystem.Setup(f => f.GetUrl(It.IsAny<string>())).Returns((string path) => "/media/" + path);
        return fileSystem;
    }

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

    private async Task MigrateAsync()
    {
        await using var context = _database.CreateDbContext();
        await context.Database.MigrateAsync();
    }

    /// <summary>An entry a member added: on their profile at once.</summary>
    private async Task AddEntryAsync(string? imagePath = null)
    {
        await MigrateAsync();
        var profile = await new MemberProfileStore(_database).GetOrStartOnboardingAsync(Guid.NewGuid(), "octocat", "The Octocat");
        var change = await _service.AddAsync(
            profile.Id, new AddActivityEntryRequest("blog", "My post", "https://example.dev/a", new DateOnly(2026, 10, 1), null), imagePath);
        _entryId = change.Entry!.Id;
    }

    /// <summary>An entry added while entries were still checked first, and never decided.</summary>
    private async Task AddLegacyWaitingEntryAsync()
    {
        await MigrateAsync();
        var profile = await new MemberProfileStore(_database).GetOrStartOnboardingAsync(Guid.NewGuid(), "octocat", "The Octocat");
        await using var context = _database.CreateDbContext();
        var entity = new MemberActivityEntryEntity
        {
            MemberProfileId = profile.Id,
            Kind = "blog",
            Title = "Old post",
            Url = "https://example.dev/old",
            DateUtc = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            State = ActivityEntryState.Pending,
            CreatedUtc = DateTime.UtcNow.AddDays(-30),
        };
        context.MemberActivityEntries.Add(entity);
        await context.SaveChangesAsync();
        _entryId = entity.Id;
    }

    [Theory]
    [InlineData(null)]
    [InlineData("editor")]
    public async Task A_user_outside_the_moderator_group_gets_403_and_cannot_remove_or_restore(string? group)
    {
        await AddEntryAsync();
        var controller = ControllerFor(group);

        (await controller.List()).Result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(403);
        (await controller.Verify(_entryId)).Result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(403);
        (await controller.Reject(_entryId, new RejectActivityEntryRequest("No."))).Result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(403);

        (await _service.ListAsync(ActivityEntryState.Verified, 0, 10)).Total.Should().Be(1);
    }

    [Fact]
    public async Task A_moderator_sees_what_members_added_by_default_and_removes_it_with_a_note()
    {
        await AddEntryAsync();
        var controller = ControllerFor(ProfileMatchReviewApiConstants.ReviewerGroupAlias, "Rita");

        var list = (await controller.List()).Value!;
        list.Total.Should().Be(1);
        list.Items.Single().Handle.Should().Be("octocat");
        list.Items.Single().State.Should().Be("Verified");

        var removed = (await controller.Reject(_entryId, new RejectActivityEntryRequest("Not your page."))).Value!;
        removed.State.Should().Be("Rejected");
        removed.RejectionReason.Should().Be("Not your page.");
        removed.DecidedByUserName.Should().Be("Rita");
        (await controller.List()).Value!.Total.Should().Be(0);
        (await controller.List(ActivityEntryState.Rejected)).Value!.Items.Should().ContainSingle(e => e.Id == _entryId);
    }

    [Fact]
    public async Task Entries_are_listed_newest_first()
    {
        await MigrateAsync();
        var profile = await new MemberProfileStore(_database).GetOrStartOnboardingAsync(Guid.NewGuid(), "octocat", "The Octocat");
        var first = await _service.AddAsync(profile.Id, new AddActivityEntryRequest("blog", "First", "https://example.dev/1", new DateOnly(2026, 10, 1), null));
        var second = await _service.AddAsync(profile.Id, new AddActivityEntryRequest("blog", "Second", "https://example.dev/2", new DateOnly(2026, 9, 1), null));

        (await ControllerFor(ProfileMatchReviewApiConstants.ReviewerGroupAlias).List()).Value!.Items.Select(e => e.Id)
            .Should().Equal(second.Entry!.Id, first.Entry!.Id);
    }

    [Fact]
    public async Task A_removed_entry_can_be_restored()
    {
        await AddEntryAsync();
        var controller = ControllerFor(ProfileMatchReviewApiConstants.ReviewerGroupAlias, "Rita");
        await controller.Reject(_entryId, new RejectActivityEntryRequest("Hmm."));

        var restored = (await controller.Verify(_entryId)).Value!;

        restored.State.Should().Be("Verified");
        restored.RejectionReason.Should().BeNull();
        (await controller.Verify(_entryId)).Result.Should().BeOfType<NotFoundResult>();
        (await controller.List()).Value!.Total.Should().Be(1);
    }

    [Fact]
    public async Task A_legacy_waiting_entry_is_listed_on_its_own_and_can_be_shown_or_removed()
    {
        await AddLegacyWaitingEntryAsync();
        var controller = ControllerFor(ProfileMatchReviewApiConstants.ReviewerGroupAlias);

        (await controller.List()).Value!.Total.Should().Be(0);
        (await controller.List(ActivityEntryState.Pending)).Value!.Items.Single().State.Should().Be("Pending");

        (await controller.Verify(_entryId)).Value!.State.Should().Be("Verified");
        (await controller.List(ActivityEntryState.Pending)).Value!.Total.Should().Be(0);
        (await controller.List()).Value!.Total.Should().Be(1);
    }

    [Fact]
    public async Task A_moderator_sees_the_picture_the_member_attached()
    {
        await AddEntryAsync("profile-activity/0123456789abcdef0123456789abcdef.png");
        var controller = ControllerFor(ProfileMatchReviewApiConstants.ReviewerGroupAlias);

        (await controller.List()).Value!.Items.Single().ImageUrl.Should().Be("/media/profile-activity/0123456789abcdef0123456789abcdef.png");
        (await controller.Reject(_entryId, new RejectActivityEntryRequest("No."))).Value!.ImageUrl.Should().NotBeNull();
    }

    [Fact]
    public async Task An_entry_without_a_picture_has_no_picture_address()
    {
        await AddEntryAsync();

        (await ControllerFor(ProfileMatchReviewApiConstants.ReviewerGroupAlias).List()).Value!.Items.Single().ImageUrl.Should().BeNull();
    }

    [Fact]
    public async Task Removing_needs_a_note_and_the_note_sticks()
    {
        await AddEntryAsync();
        var controller = ControllerFor(ProfileMatchReviewApiConstants.ReviewerGroupAlias);

        (await controller.Reject(_entryId, new RejectActivityEntryRequest("  "))).Result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(400);
        (await _service.ListAsync(ActivityEntryState.Verified, 0, 10)).Total.Should().Be(1);

        var removed = (await controller.Reject(_entryId, new RejectActivityEntryRequest("Not your page."))).Value!;

        removed.State.Should().Be("Rejected");
        removed.RejectionReason.Should().Be("Not your page.");
        (await controller.Reject(_entryId, new RejectActivityEntryRequest("Again."))).Result.Should().BeOfType<NotFoundResult>();

        var restored = (await controller.Verify(_entryId)).Value!;
        restored.State.Should().Be("Verified");
        restored.RejectionReason.Should().BeNull();

        var removedAgain = (await controller.Reject(_entryId, new RejectActivityEntryRequest("Changed my mind."))).Value!;
        removedAgain.State.Should().Be("Rejected");
        removedAgain.RejectionReason.Should().Be("Changed my mind.");
    }
}

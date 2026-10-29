using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Moq;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Routing;
using UmbracoCommunity.Web.Features.Profiles;
using UmbracoCommunity.Web.Features.Profiles.Data;
using UmbracoCommunity.Web.Features.Profiles.Data.Entities;
using Xunit;

namespace UmbracoCommunity.Web.Tests.Features.Profiles;

public class MemberProfileVisibilityTests : IDisposable
{
    private const string Handle = "octocat";

    private readonly MemberProfilesTestDatabase _database = new();
    private readonly MemberProfileStore _store;
    private readonly MemberProfileDataProvider _provider;

    public MemberProfileVisibilityTests()
    {
        _store = new MemberProfileStore(_database);
        _provider = new MemberProfileDataProvider(
            _store,
            new ProfileAvatarUrlResolver(Mock.Of<IPublishedContentQuery>(), Mock.Of<IPublishedUrlProvider>()));
    }

    public void Dispose() => _database.Dispose();

    private async Task MigrateAsync()
    {
        await using var context = _database.CreateDbContext();
        await context.Database.MigrateAsync();
    }

    private async Task<Guid> CreateCompletedProfileAsync()
    {
        await MigrateAsync();

        var memberKey = Guid.NewGuid();
        await _store.GetOrStartOnboardingAsync(memberKey, Handle, "The Octocat");
        await _store.CompleteOnboardingAsync(memberKey);
        return memberKey;
    }

    [Fact]
    public async Task A_new_profile_starts_hidden()
    {
        var memberKey = await CreateCompletedProfileAsync();

        (await _store.GetByMemberKeyAsync(memberKey))!.IsProfileVisible.Should().BeFalse();
        (await _provider.GetProfileAsync(Handle)).Should().BeNull();
    }

    [Fact]
    public async Task A_hidden_profile_is_returned_only_when_hidden_profiles_are_requested()
    {
        await CreateCompletedProfileAsync();

        (await _provider.GetProfileAsync(Handle, includeHidden: false)).Should().BeNull();

        var profile = await _provider.GetProfileAsync(Handle, includeHidden: true);
        profile.Should().NotBeNull();
        profile!.Slug.Should().Be(Handle);
    }

    [Fact]
    public async Task Showing_a_profile_makes_it_visible_to_everyone()
    {
        var memberKey = await CreateCompletedProfileAsync();

        var updated = await _store.SetProfileVisibleAsync(memberKey, true);

        updated.Should().BeTrue();
        (await _provider.GetProfileAsync(Handle)).Should().NotBeNull();
    }

    [Fact]
    public async Task Hiding_a_shown_profile_returns_it_as_null_again()
    {
        var memberKey = await CreateCompletedProfileAsync();
        await _store.SetProfileVisibleAsync(memberKey, true);

        await _store.SetProfileVisibleAsync(memberKey, false);

        (await _provider.GetProfileAsync(Handle)).Should().BeNull();
        (await _provider.GetProfileAsync(Handle, includeHidden: true)).Should().NotBeNull();
    }

    [Fact]
    public async Task An_unfinished_onboarding_is_never_returned_even_when_hidden_profiles_are_requested()
    {
        await MigrateAsync();
        var memberKey = Guid.NewGuid();
        await _store.GetOrStartOnboardingAsync(memberKey, Handle, "The Octocat");
        await _store.SetProfileVisibleAsync(memberKey, true);

        (await _provider.GetProfileAsync(Handle, includeHidden: true)).Should().BeNull();
    }

    [Fact]
    public async Task Changing_visibility_for_an_unknown_member_reports_no_change()
    {
        await CreateCompletedProfileAsync();

        (await _store.SetProfileVisibleAsync(Guid.NewGuid(), true)).Should().BeFalse();
        (await _provider.GetProfileAsync(Handle)).Should().BeNull();
    }

    [Fact]
    public async Task The_migration_keeps_existing_profiles_visible()
    {
        await using (var context = _database.CreateDbContext())
        {
            var migrator = context.GetService<IMigrator>();
            await migrator.MigrateAsync("20260722100000_RenamePlatformSyncColumns");

            await context.Database.ExecuteSqlRawAsync(
                "INSERT INTO MemberProfiles (MemberKey, GitHubHandle, DisplayName, OnboardingStatus, CreatedUtc, UpdatedUtc) " +
                "VALUES ({0}, {1}, {2}, {3}, {4}, {5})",
                Guid.NewGuid(), Handle, "The Octocat", (int)OnboardingStatus.Completed, DateTime.UtcNow, DateTime.UtcNow);

            await context.Database.MigrateAsync();
        }

        await using (var context = _database.CreateDbContext())
        {
            var entity = await context.MemberProfiles.SingleAsync();
            entity.IsProfileVisible.Should().BeTrue();
        }

        (await _provider.GetProfileAsync(Handle)).Should().NotBeNull();

        // A profile created after the migration still starts hidden.
        var newMemberKey = Guid.NewGuid();
        await _store.GetOrStartOnboardingAsync(newMemberKey, "hubot", "Hubot");
        await _store.CompleteOnboardingAsync(newMemberKey);
        (await _store.GetByMemberKeyAsync(newMemberKey))!.IsProfileVisible.Should().BeFalse();
        (await _provider.GetProfileAsync("hubot")).Should().BeNull();
    }
}

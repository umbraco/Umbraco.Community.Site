using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using UmbracoCommunity.Web.Features.Profiles;
using UmbracoCommunity.Web.Features.Profiles.Data;
using UmbracoCommunity.Web.Features.Profiles.Data.Entities;
using UmbracoCommunity.Web.Tests.Features.Feeds.Calendar;
using Xunit;

namespace UmbracoCommunity.Web.Tests.Features.Profiles;

public class ProfileAutoOnboardingServiceTests : IAsyncLifetime, IDisposable
{
    private const string Handle = "octocat";
    private const string GitHubName = "The Octocat";
    private const string GitHubProfileJson = """{ "bio": "Bio from GitHub", "company": "@umbraco" }""";

    private readonly MemberProfilesTestDatabase _database = new();
    private readonly MemberProfileStore _store;
    private readonly Guid _memberKey = Guid.NewGuid();

    public ProfileAutoOnboardingServiceTests()
    {
        _store = new MemberProfileStore(_database);
    }

    public async Task InitializeAsync()
    {
        await using var context = _database.CreateDbContext();
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose() => _database.Dispose();

    private ProfileAutoOnboardingService CreateService(StubHandler handler) =>
        new(
            _store,
            new GitHubProfileClient(
                new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") },
                NullLogger<GitHubProfileClient>.Instance),
            NullLogger<ProfileAutoOnboardingService>.Instance);

    [Fact]
    public async Task Creates_the_profile_fills_name_headline_and_bio_and_completes_onboarding()
    {
        var onboarded = await CreateService(StubHandler.Json(GitHubProfileJson))
            .EnsureOnboardedAsync(_memberKey, Handle, GitHubName);

        onboarded.Should().BeTrue();
        var stored = await _store.GetByMemberKeyAsync(_memberKey);
        stored!.OnboardingStatus.Should().Be(OnboardingStatus.Completed);
        stored.OnboardingCompletedUtc.Should().NotBeNull();
        stored.GitHubHandle.Should().Be(Handle);
        stored.DisplayNameOverride.Should().Be(GitHubName);
        stored.Headline.Should().Be("umbraco");
        stored.Bio.Should().Be("Bio from GitHub");
        stored.GitHubBio.Should().Be("Bio from GitHub");
        stored.GitHubCompany.Should().Be("umbraco");
        stored.GitHubFetchedUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Uses_the_handle_when_the_member_has_no_GitHub_name()
    {
        await CreateService(StubHandler.Json(GitHubProfileJson)).EnsureOnboardedAsync(_memberKey, Handle, "  ");

        (await _store.GetByMemberKeyAsync(_memberKey))!.DisplayNameOverride.Should().Be(Handle);
    }

    [Fact]
    public async Task Cuts_a_long_GitHub_name_to_the_display_name_limit()
    {
        await CreateService(StubHandler.Json(GitHubProfileJson))
            .EnsureOnboardedAsync(_memberKey, Handle, new string('a', ProfileText.MaxDisplayNameLength + 20));

        (await _store.GetByMemberKeyAsync(_memberKey))!.DisplayNameOverride.Should().HaveLength(ProfileText.MaxDisplayNameLength);
    }

    [Fact]
    public async Task Keeps_text_the_member_already_wrote()
    {
        await _store.GetOrStartOnboardingAsync(_memberKey, Handle, GitHubName);
        await _store.UpdateDisplayNameAsync(_memberKey, "My own name");
        await _store.UpdateBioAsync(_memberKey, "My own bio");

        var onboarded = await CreateService(StubHandler.Json(GitHubProfileJson))
            .EnsureOnboardedAsync(_memberKey, Handle, GitHubName);

        onboarded.Should().BeTrue();
        var stored = await _store.GetByMemberKeyAsync(_memberKey);
        stored!.DisplayNameOverride.Should().Be("My own name");
        stored.Bio.Should().Be("My own bio");
        stored.Headline.Should().Be("umbraco");
        stored.OnboardingStatus.Should().Be(OnboardingStatus.Completed);
    }

    [Fact]
    public async Task A_failed_GitHub_lookup_still_completes_onboarding_with_the_name()
    {
        var onboarded = await CreateService(StubHandler.Throws()).EnsureOnboardedAsync(_memberKey, Handle, GitHubName);

        onboarded.Should().BeTrue();
        var stored = await _store.GetByMemberKeyAsync(_memberKey);
        stored!.OnboardingStatus.Should().Be(OnboardingStatus.Completed);
        stored.DisplayNameOverride.Should().Be(GitHubName);
        stored.Headline.Should().BeNull();
        stored.Bio.Should().BeNull();
        stored.GitHubFetchedUtc.Should().BeNull();
    }

    [Fact]
    public async Task Does_nothing_for_a_completed_profile()
    {
        await _store.GetOrStartOnboardingAsync(_memberKey, Handle, GitHubName);
        await _store.CompleteOnboardingAsync(_memberKey);
        var handler = StubHandler.Json(GitHubProfileJson);

        var onboarded = await CreateService(handler).EnsureOnboardedAsync(_memberKey, Handle, GitHubName);

        onboarded.Should().BeFalse();
        handler.CallCount.Should().Be(0);
        var stored = await _store.GetByMemberKeyAsync(_memberKey);
        stored!.DisplayNameOverride.Should().BeNull();
        stored.Headline.Should().BeNull();
        stored.Bio.Should().BeNull();
    }

    [Fact]
    public async Task Leaves_the_profile_hidden()
    {
        await CreateService(StubHandler.Json(GitHubProfileJson)).EnsureOnboardedAsync(_memberKey, Handle, GitHubName);

        (await _store.GetByMemberKeyAsync(_memberKey))!.IsProfileVisible.Should().BeFalse();
    }
}

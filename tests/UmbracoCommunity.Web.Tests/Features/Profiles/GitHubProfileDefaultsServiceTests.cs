using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using UmbracoCommunity.Web.Features.Profiles;
using UmbracoCommunity.Web.Features.Profiles.Data;
using UmbracoCommunity.Web.Features.Profiles.Data.Entities;
using UmbracoCommunity.Web.Tests.Features.Feeds.Calendar;
using Xunit;

namespace UmbracoCommunity.Web.Tests.Features.Profiles;

public class GitHubProfileDefaultsServiceTests : IDisposable
{
    private const string Handle = "octocat";
    private const string GitHubProfileJson = """{ "bio": "Bio from GitHub", "company": "@umbraco" }""";

    private readonly MemberProfilesTestDatabase _database = new();
    private readonly MemberProfileStore _store;

    public GitHubProfileDefaultsServiceTests()
    {
        _store = new MemberProfileStore(_database);
    }

    public void Dispose() => _database.Dispose();

    private GitHubProfileDefaultsService CreateService(StubHandler handler) =>
        new(
            new GitHubProfileClient(
                new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") },
                NullLogger<GitHubProfileClient>.Instance),
            _store,
            NullLogger<GitHubProfileDefaultsService>.Instance);

    private async Task<MemberProfileEntity> CreateProfileAsync()
    {
        await using (var context = _database.CreateDbContext())
        {
            await context.Database.MigrateAsync();
        }

        return await _store.GetOrStartOnboardingAsync(Guid.NewGuid(), Handle, "The Octocat");
    }

    [Fact]
    public async Task Fetches_and_stores_the_GitHub_bio_and_company()
    {
        var profile = await CreateProfileAsync();
        var service = CreateService(StubHandler.Json(GitHubProfileJson));

        await service.EnsureGitHubDefaultsAsync(profile);

        profile.GitHubBio.Should().Be("Bio from GitHub");
        profile.GitHubCompany.Should().Be("umbraco");
        profile.GitHubFetchedUtc.Should().NotBeNull();

        var stored = await _store.GetByMemberKeyAsync(profile.MemberKey);
        stored!.GitHubBio.Should().Be("Bio from GitHub");
        stored.GitHubCompany.Should().Be("umbraco");
        stored.GitHubFetchedUtc.Should().NotBeNull();
        stored.Bio.Should().BeNull();
        stored.Headline.Should().BeNull();
    }

    [Fact]
    public async Task Fetches_only_once()
    {
        await CreateProfileAsync();
        var handler = StubHandler.Json(GitHubProfileJson);
        var service = CreateService(handler);

        await service.EnsureGitHubDefaultsAsync((await _store.GetByHandleAsync(Handle))!);
        await service.EnsureGitHubDefaultsAsync((await _store.GetByHandleAsync(Handle))!);

        handler.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task A_user_without_a_bio_or_company_is_remembered_as_fetched()
    {
        await CreateProfileAsync();
        var handler = StubHandler.Status(HttpStatusCode.NotFound);
        var service = CreateService(handler);

        await service.EnsureGitHubDefaultsAsync((await _store.GetByHandleAsync(Handle))!);
        await service.EnsureGitHubDefaultsAsync((await _store.GetByHandleAsync(Handle))!);

        handler.CallCount.Should().Be(1);
        var stored = await _store.GetByHandleAsync(Handle);
        stored!.GitHubBio.Should().BeNull();
        stored.GitHubCompany.Should().BeNull();
        stored.GitHubFetchedUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task A_failed_fetch_is_not_stored_and_is_tried_again()
    {
        var profile = await CreateProfileAsync();

        await CreateService(StubHandler.Throws()).EnsureGitHubDefaultsAsync(profile);

        profile.GitHubFetchedUtc.Should().BeNull();
        (await _store.GetByHandleAsync(Handle))!.GitHubFetchedUtc.Should().BeNull();

        await CreateService(StubHandler.Json(GitHubProfileJson)).EnsureGitHubDefaultsAsync(profile);

        var stored = await _store.GetByHandleAsync(Handle);
        stored!.GitHubBio.Should().Be("Bio from GitHub");
        stored.GitHubCompany.Should().Be("umbraco");
    }
}

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Routing;
using UmbracoCommunity.Web.Features.Profiles;
using UmbracoCommunity.Web.Features.Profiles.Data;
using Xunit;

namespace UmbracoCommunity.Web.Tests.Features.Profiles;

public class MemberProfileDetailsTests : IDisposable
{
    private const string Handle = "octocat";

    private readonly MemberProfilesTestDatabase _database = new();
    private readonly MemberProfileStore _store;
    private readonly MemberProfileDataProvider _provider;

    public MemberProfileDetailsTests()
    {
        _store = new MemberProfileStore(_database);
        _provider = new MemberProfileDataProvider(
            _store,
            new ProfileAvatarUrlResolver(Mock.Of<IPublishedContentQuery>(), Mock.Of<IPublishedUrlProvider>()),
            TestImageProxy.Create());
    }

    public void Dispose() => _database.Dispose();

    private async Task<Guid> CreateVisibleProfileAsync()
    {
        await using (var context = _database.CreateDbContext())
        {
            await context.Database.MigrateAsync();
        }

        var memberKey = Guid.NewGuid();
        await _store.GetOrStartOnboardingAsync(memberKey, Handle, "The Octocat");
        await _store.CompleteOnboardingAsync(memberKey);
        await _store.SetProfileVisibleAsync(memberKey, true);
        return memberKey;
    }

    [Fact]
    public async Task A_headline_is_stored_trimmed_and_shown_on_the_profile()
    {
        var memberKey = await CreateVisibleProfileAsync();

        var updated = await _store.UpdateHeadlineAsync(memberKey, "  Umbraco developer  ");

        updated!.Headline.Should().Be("Umbraco developer");
        (await _provider.GetProfileAsync(Handle))!.Identity.Headline.Should().Be("Umbraco developer");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Clearing_the_headline_stores_null(string? headline)
    {
        var memberKey = await CreateVisibleProfileAsync();
        await _store.UpdateHeadlineAsync(memberKey, "Umbraco developer");

        var updated = await _store.UpdateHeadlineAsync(memberKey, headline);

        updated!.Headline.Should().BeNull();
        (await _store.GetByMemberKeyAsync(memberKey))!.Headline.Should().BeNull();
        (await _provider.GetProfileAsync(Handle))!.Identity.Headline.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \n ")]
    public async Task Clearing_the_bio_stores_null(string? bio)
    {
        var memberKey = await CreateVisibleProfileAsync();
        await _store.UpdateBioAsync(memberKey, "My own words");

        var updated = await _store.UpdateBioAsync(memberKey, bio);

        updated!.Bio.Should().BeNull();
        (await _store.GetByMemberKeyAsync(memberKey))!.Bio.Should().BeNull();
    }

    [Fact]
    public async Task Updating_an_unknown_member_returns_null()
    {
        await CreateVisibleProfileAsync();

        (await _store.UpdateHeadlineAsync(Guid.NewGuid(), "Hello")).Should().BeNull();
        (await _store.UpdateDisplayNameAsync(Guid.NewGuid(), "Hello")).Should().BeNull();
        (await _store.UpdateBioAsync(Guid.NewGuid(), "Hello")).Should().BeNull();
    }

    [Fact]
    public async Task The_profile_shows_the_GitHub_bio_when_the_member_has_no_bio_of_their_own()
    {
        var memberKey = await CreateVisibleProfileAsync();

        await _store.SetGitHubDefaultsAsync(memberKey, "Bio from GitHub", null, DateTime.UtcNow);

        (await _provider.GetProfileAsync(Handle))!.Identity.Bio.Should().Be("Bio from GitHub");
    }

    [Fact]
    public async Task The_members_own_bio_wins_over_the_GitHub_bio()
    {
        var memberKey = await CreateVisibleProfileAsync();
        await _store.SetGitHubDefaultsAsync(memberKey, "Bio from GitHub", null, DateTime.UtcNow);

        await _store.UpdateBioAsync(memberKey, "My own words");

        (await _provider.GetProfileAsync(Handle))!.Identity.Bio.Should().Be("My own words");
    }

    [Fact]
    public async Task Clearing_the_bio_shows_the_GitHub_bio_again()
    {
        var memberKey = await CreateVisibleProfileAsync();
        await _store.SetGitHubDefaultsAsync(memberKey, "Bio from GitHub", null, DateTime.UtcNow);
        await _store.UpdateBioAsync(memberKey, "My own words");

        await _store.UpdateBioAsync(memberKey, "");

        (await _provider.GetProfileAsync(Handle))!.Identity.Bio.Should().Be("Bio from GitHub");
    }

    [Fact]
    public async Task A_profile_with_no_bio_anywhere_has_a_null_bio()
    {
        await CreateVisibleProfileAsync();

        (await _provider.GetProfileAsync(Handle))!.Identity.Bio.Should().BeNull();
    }

    [Fact]
    public async Task Details_do_not_make_a_hidden_profile_visible()
    {
        var memberKey = await CreateVisibleProfileAsync();
        await _store.SetProfileVisibleAsync(memberKey, false);

        await _store.UpdateHeadlineAsync(memberKey, "Umbraco developer");
        await _store.UpdateBioAsync(memberKey, "My own words");

        (await _provider.GetProfileAsync(Handle)).Should().BeNull();
        (await _provider.GetProfileAsync(Handle, includeHidden: true))!.Identity.Headline.Should().Be("Umbraco developer");
    }

    [Fact]
    public async Task The_profile_shows_the_GitHub_company_as_the_headline_when_the_member_has_none_of_their_own()
    {
        var memberKey = await CreateVisibleProfileAsync();

        await _store.SetGitHubDefaultsAsync(memberKey, null, "umbraco", DateTime.UtcNow);

        (await _provider.GetProfileAsync(Handle))!.Identity.Headline.Should().Be("umbraco");
    }

    [Fact]
    public async Task The_members_own_headline_wins_and_clearing_it_shows_the_GitHub_company_again()
    {
        var memberKey = await CreateVisibleProfileAsync();
        await _store.SetGitHubDefaultsAsync(memberKey, null, "umbraco", DateTime.UtcNow);

        await _store.UpdateHeadlineAsync(memberKey, "Umbraco developer");
        (await _provider.GetProfileAsync(Handle))!.Identity.Headline.Should().Be("Umbraco developer");

        await _store.UpdateHeadlineAsync(memberKey, " ");
        (await _provider.GetProfileAsync(Handle))!.Identity.Headline.Should().Be("umbraco");
    }

    [Fact]
    public async Task A_display_name_is_stored_trimmed_and_shown_on_the_profile()
    {
        var memberKey = await CreateVisibleProfileAsync();

        var updated = await _store.UpdateDisplayNameAsync(memberKey, "  Octo  ");

        updated!.DisplayNameOverride.Should().Be("Octo");
        updated.DisplayName.Should().Be("The Octocat");
        (await _provider.GetProfileAsync(Handle))!.Identity.DisplayName.Should().Be("Octo");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Clearing_the_display_name_shows_the_GitHub_name_again(string? displayName)
    {
        var memberKey = await CreateVisibleProfileAsync();
        await _store.UpdateDisplayNameAsync(memberKey, "Octo");

        var updated = await _store.UpdateDisplayNameAsync(memberKey, displayName);

        updated!.DisplayNameOverride.Should().BeNull();
        updated.DisplayName.Should().Be("The Octocat");
        (await _provider.GetProfileAsync(Handle))!.Identity.DisplayName.Should().Be("The Octocat");
    }
}

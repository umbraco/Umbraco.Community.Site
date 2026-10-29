using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Umbraco.Cms.Core.Security;
using UmbracoCommunity.Web.Features.Profiles;
using UmbracoCommunity.Web.Features.Profiles.Controllers;
using UmbracoCommunity.Web.Features.Profiles.Data;
using UmbracoCommunity.Web.Features.Profiles.Models;
using Xunit;

namespace UmbracoCommunity.Web.Tests.Features.Profiles;

/// <summary>
/// The display name, headline and bio actions only use the member manager and the store, so the
/// other dependencies are left null.
/// </summary>
public class ProfileApiControllerTests : IDisposable
{
    private const string Handle = "octocat";

    private readonly MemberProfilesTestDatabase _database = new();
    private readonly MemberProfileStore _store;
    private readonly Guid _memberKey = Guid.NewGuid();

    public ProfileApiControllerTests()
    {
        _store = new MemberProfileStore(_database);
    }

    public void Dispose() => _database.Dispose();

    private async Task MigrateAsync()
    {
        await using var context = _database.CreateDbContext();
        await context.Database.MigrateAsync();
    }

    private async Task CreateProfileAsync()
    {
        await MigrateAsync();
        await _store.GetOrStartOnboardingAsync(_memberKey, Handle, "The Octocat");
    }

    private ProfileApiController CreateController(bool signedIn = true)
    {
        var memberManager = new Mock<IMemberManager>();
        memberManager
            .Setup(m => m.GetCurrentMemberAsync())
            .ReturnsAsync(signedIn ? new MemberIdentityUser { Key = _memberKey, UserName = Handle } : null);

        return new ProfileApiController(memberManager.Object, _store, null!, null!, null!, null!);
    }

    private static ProfileDetailsResponse Details(IActionResult result) =>
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeOfType<ProfileDetailsResponse>().Subject;

    [Fact]
    public async Task A_headline_over_the_limit_is_rejected()
    {
        await CreateProfileAsync();

        var result = await CreateController().UpdateHeadline(new UpdateHeadlineRequest(new string('a', ProfileText.MaxHeadlineLength + 1)), default);

        result.Should().BeOfType<BadRequestObjectResult>();
        (await _store.GetByMemberKeyAsync(_memberKey))!.Headline.Should().BeNull();
    }

    [Fact]
    public async Task A_headline_at_the_limit_is_saved()
    {
        await CreateProfileAsync();
        var headline = new string('a', ProfileText.MaxHeadlineLength);

        var result = await CreateController().UpdateHeadline(new UpdateHeadlineRequest(headline), default);

        var details = Details(result);
        details.Headline.Should().Be(headline);
        details.HasCustomHeadline.Should().BeTrue();
    }

    [Fact]
    public async Task Surrounding_whitespace_does_not_count_towards_the_headline_limit()
    {
        await CreateProfileAsync();
        var headline = new string('a', ProfileText.MaxHeadlineLength);

        var result = await CreateController().UpdateHeadline(new UpdateHeadlineRequest($"  {headline}  "), default);

        Details(result).Headline.Should().Be(headline);
    }

    [Fact]
    public async Task A_whitespace_headline_is_stored_as_null_and_the_GitHub_company_is_returned()
    {
        await CreateProfileAsync();
        await _store.SetGitHubDefaultsAsync(_memberKey, null, "umbraco", DateTime.UtcNow);
        await _store.UpdateHeadlineAsync(_memberKey, "Umbraco developer");

        var result = await CreateController().UpdateHeadline(new UpdateHeadlineRequest("   "), default);

        var details = Details(result);
        details.Headline.Should().Be("umbraco");
        details.HasCustomHeadline.Should().BeFalse();
        details.GitHubHeadline.Should().Be("umbraco");
        (await _store.GetByMemberKeyAsync(_memberKey))!.Headline.Should().BeNull();
    }

    [Fact]
    public async Task Updating_the_headline_when_signed_out_is_unauthorized()
    {
        await CreateProfileAsync();

        var result = await CreateController(signedIn: false).UpdateHeadline(new UpdateHeadlineRequest("Hello"), default);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task Updating_the_headline_without_a_profile_is_rejected()
    {
        await MigrateAsync();

        var result = await CreateController().UpdateHeadline(new UpdateHeadlineRequest("Hello"), default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task A_bio_over_the_limit_is_rejected()
    {
        await CreateProfileAsync();

        var result = await CreateController().UpdateBio(new UpdateBioRequest(new string('a', ProfileText.MaxBioLength + 1)), default);

        result.Should().BeOfType<BadRequestObjectResult>();
        (await _store.GetByMemberKeyAsync(_memberKey))!.Bio.Should().BeNull();
    }

    [Fact]
    public async Task An_empty_bio_is_stored_as_null_and_the_GitHub_bio_is_returned()
    {
        await CreateProfileAsync();
        await _store.SetGitHubDefaultsAsync(_memberKey, "Bio from GitHub", null, DateTime.UtcNow);
        await _store.UpdateBioAsync(_memberKey, "My own words");

        var result = await CreateController().UpdateBio(new UpdateBioRequest(""), default);

        var details = Details(result);
        details.Bio.Should().Be("Bio from GitHub");
        details.HasCustomBio.Should().BeFalse();
        (await _store.GetByMemberKeyAsync(_memberKey))!.Bio.Should().BeNull();
    }

    [Fact]
    public async Task A_display_name_over_the_limit_is_rejected()
    {
        await CreateProfileAsync();

        var result = await CreateController().UpdateDisplayName(new UpdateDisplayNameRequest(new string('a', ProfileText.MaxDisplayNameLength + 1)), default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task A_display_name_is_saved_and_clearing_it_returns_the_GitHub_name()
    {
        await CreateProfileAsync();
        var controller = CreateController();

        var saved = Details(await controller.UpdateDisplayName(new UpdateDisplayNameRequest(" Octo "), default));
        saved.DisplayName.Should().Be("Octo");
        saved.HasCustomDisplayName.Should().BeTrue();
        saved.DefaultDisplayName.Should().Be("The Octocat");

        var cleared = Details(await controller.UpdateDisplayName(new UpdateDisplayNameRequest(""), default));
        cleared.DisplayName.Should().Be("The Octocat");
        cleared.HasCustomDisplayName.Should().BeFalse();
        (await _store.GetByMemberKeyAsync(_memberKey))!.DisplayName.Should().Be("The Octocat");
    }
}

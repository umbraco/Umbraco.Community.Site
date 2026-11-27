using FluentAssertions;
using Microsoft.AspNetCore.Http;
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

    private ProfileApiController CreateController(bool signedIn = true, string? editAsHeader = null)
    {
        var memberManager = new Mock<IMemberManager>();
        memberManager
            .Setup(m => m.GetCurrentMemberAsync())
            .ReturnsAsync(signedIn ? new MemberIdentityUser { Key = _memberKey, UserName = Handle } : null);

        var controller = new ProfileApiController(memberManager.Object, _store, null!, null!);
        if (editAsHeader != null)
        {
            // The header the removed "edit as" feature used to send: it must not change whose profile is edited.
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers["X-Profile-Edit-As"] = editAsHeader;
            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        }

        return controller;
    }

    [Fact]
    public async Task A_request_naming_another_member_only_ever_changes_the_signed_in_members_profile()
    {
        await CreateProfileAsync();
        var otherKey = Guid.NewGuid();
        await _store.GetOrStartOnboardingAsync(otherKey, "hubot", "Hubot");

        var result = await CreateController(editAsHeader: "hubot").UpdateBio(new UpdateBioRequest("Mine"), default);

        Details(result).Bio.Should().Be("Mine");
        (await _store.GetByMemberKeyAsync(_memberKey))!.Bio.Should().Be("Mine");
        (await _store.GetByMemberKeyAsync(otherKey))!.Bio.Should().BeNull();
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

        Details(result).Headline.Should().Be(headline);
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
    public async Task An_empty_headline_clears_it_and_never_falls_back_to_the_GitHub_company()
    {
        await CreateProfileAsync();
        await _store.SetGitHubDefaultsAsync(_memberKey, null, "umbraco", DateTime.UtcNow);
        await _store.UpdateHeadlineAsync(_memberKey, "Umbraco developer");

        var result = await CreateController().UpdateHeadline(new UpdateHeadlineRequest("   "), default);

        Details(result).Headline.Should().BeNull();
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
    public async Task An_empty_bio_clears_it_and_never_falls_back_to_the_GitHub_bio()
    {
        await CreateProfileAsync();
        await _store.SetGitHubDefaultsAsync(_memberKey, "Bio from GitHub", null, DateTime.UtcNow);
        await _store.UpdateBioAsync(_memberKey, "My own words");

        var result = await CreateController().UpdateBio(new UpdateBioRequest(""), default);

        Details(result).Bio.Should().BeNull();
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
    public async Task A_display_name_is_saved_trimmed()
    {
        await CreateProfileAsync();

        var saved = Details(await CreateController().UpdateDisplayName(new UpdateDisplayNameRequest(" Octo "), default));

        saved.DisplayName.Should().Be("Octo");
        (await _store.GetByMemberKeyAsync(_memberKey))!.DisplayNameOverride.Should().Be("Octo");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task An_empty_display_name_is_rejected_and_the_name_is_kept(string? displayName)
    {
        await CreateProfileAsync();
        var controller = CreateController();
        await controller.UpdateDisplayName(new UpdateDisplayNameRequest("Octo"), default);

        var result = await controller.UpdateDisplayName(new UpdateDisplayNameRequest(displayName), default);

        result.Should().BeOfType<BadRequestObjectResult>()
            .Which.Value.Should().BeEquivalentTo(new { error = "Please enter your name. It can't be empty." });
        (await _store.GetByMemberKeyAsync(_memberKey))!.EffectiveDisplayName.Should().Be("Octo");
    }
}

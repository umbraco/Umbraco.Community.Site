using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Security;
using UmbracoCommunity.Web.Features.Profiles;
using UmbracoCommunity.Web.Features.Profiles.Controllers;
using UmbracoCommunity.Web.Features.Profiles.Data;
using UmbracoCommunity.Web.Features.Profiles.Models;
using UmbracoCommunity.Web.Models.Pages;
using Xunit;

namespace UmbracoCommunity.Web.Tests.Features.Profiles;

public class ProfileEditAsTests : IDisposable
{
    private const string AdminHandle = "octocat";
    private const string TargetHandle = "hubot";

    private readonly MemberProfilesTestDatabase _database = new();
    private readonly MemberProfileStore _store;
    private readonly MemberIdentityUser _admin = new() { Id = Guid.NewGuid().ToString(), Key = Guid.NewGuid(), UserName = AdminHandle, Email = "admin@example.com" };
    private readonly MemberIdentityUser _target = new() { Id = Guid.NewGuid().ToString(), Key = Guid.NewGuid(), UserName = TargetHandle, Email = "target@example.com" };
    private readonly CapturingLogger<ProfileEditSubjectResolver> _log = new();

    public ProfileEditAsTests()
    {
        _store = new MemberProfileStore(_database);
    }

    public void Dispose() => _database.Dispose();

    private async Task ProfilesAsync()
    {
        await using (var context = _database.CreateDbContext())
        {
            await context.Database.MigrateAsync();
        }

        foreach (var member in new[] { _admin, _target })
        {
            await _store.GetOrStartOnboardingAsync(member.Key, member.UserName!, member.UserName!);
            await _store.CompleteOnboardingAsync(member.Key);
        }
    }

    private ProfileEditSubjectResolver Resolver(bool isAdmin)
    {
        var memberManager = new Mock<IMemberManager>();
        memberManager.Setup(m => m.IsLoggedIn()).Returns(true);
        memberManager.Setup(m => m.GetCurrentMemberAsync()).ReturnsAsync(_admin);
        memberManager.Setup(m => m.GetRolesAsync(_admin))
            .ReturnsAsync(isAdmin ? [ProfileAccessService.DevRelGroupName] : ["Community Members"]);
        memberManager.Setup(m => m.GetRolesAsync(_target)).ReturnsAsync(["Community Members"]);
        memberManager.Setup(m => m.FindByNameAsync(TargetHandle)).ReturnsAsync(_target);

        var access = new ProfileAccessService(memberManager.Object, Mock.Of<IProfileDataProvider>(), NullLogger<ProfileAccessService>.Instance);
        return new ProfileEditSubjectResolver(memberManager.Object, access, _store, _log);
    }

    private ProfileApiController Controller(bool isAdmin, string? editAs)
    {
        var context = new DefaultHttpContext();
        if (editAs != null)
        {
            context.Request.Headers[ProfileEditSubjectResolver.HeaderName] = editAs;
        }

        return new ProfileApiController(Resolver(isAdmin), _store, null!, null!, null!, null!)
        {
            ControllerContext = new ControllerContext { HttpContext = context },
        };
    }

    private async Task<string?> BioAsync(MemberIdentityUser member) => (await _store.GetByMemberKeyAsync(member.Key))!.Bio;

    [Fact]
    public async Task Without_the_header_a_request_acts_on_the_signed_in_member()
    {
        await ProfilesAsync();

        var result = await Controller(isAdmin: true, editAs: null).UpdateBio(new UpdateBioRequest("Mine"), default);

        result.Should().BeOfType<OkObjectResult>();
        (await BioAsync(_admin)).Should().Be("Mine");
        (await BioAsync(_target)).Should().BeNull();
    }

    [Fact]
    public async Task Naming_another_member_without_being_a_profile_admin_is_forbidden_and_changes_nothing()
    {
        await ProfilesAsync();

        var result = await Controller(isAdmin: false, editAs: TargetHandle).UpdateBio(new UpdateBioRequest("Hijacked"), default);

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        (await BioAsync(_target)).Should().BeNull();
        (await BioAsync(_admin)).Should().BeNull();
    }

    [Theory]
    [InlineData("hubot")]
    [InlineData("HUBOT")]
    public async Task A_profile_admin_changes_the_named_members_profile_and_not_their_own(string editAs)
    {
        await ProfilesAsync();

        var result = await Controller(isAdmin: true, editAs: editAs).UpdateBio(new UpdateBioRequest("Edited by an admin"), default);

        result.Should().BeOfType<OkObjectResult>();
        (await BioAsync(_target)).Should().Be("Edited by an admin");
        (await BioAsync(_admin)).Should().BeNull();
    }

    [Theory]
    [InlineData("nobody")]
    [InlineData("")]
    public async Task An_unknown_handle_is_not_found_and_never_falls_back_to_the_admins_own_profile(string editAs)
    {
        await ProfilesAsync();

        var result = await Controller(isAdmin: true, editAs: editAs).UpdateBio(new UpdateBioRequest("Lost"), default);

        result.Should().BeOfType<NotFoundObjectResult>();
        (await BioAsync(_admin)).Should().BeNull();
    }

    [Fact]
    public async Task A_handle_whose_member_record_does_not_match_the_profile_is_not_found()
    {
        await ProfilesAsync();
        var memberManager = new Mock<IMemberManager>();
        memberManager.Setup(m => m.IsLoggedIn()).Returns(true);
        memberManager.Setup(m => m.GetCurrentMemberAsync()).ReturnsAsync(_admin);
        memberManager.Setup(m => m.GetRolesAsync(_admin)).ReturnsAsync([ProfileAccessService.DevRelGroupName]);
        memberManager.Setup(m => m.FindByNameAsync(TargetHandle)).ReturnsAsync(new MemberIdentityUser { Key = Guid.NewGuid(), UserName = TargetHandle });
        var access = new ProfileAccessService(memberManager.Object, Mock.Of<IProfileDataProvider>(), NullLogger<ProfileAccessService>.Instance);
        var resolver = new ProfileEditSubjectResolver(memberManager.Object, access, _store, _log);

        (await resolver.ResolveAsync(TargetHandle)).Failure.Should().Be(ProfileEditSubjectFailure.NotFound);
    }

    [Fact]
    public async Task A_profile_admin_can_edit_a_profile_that_has_no_umbraco_member()
    {
        await ProfilesAsync();
        var memberlessKey = Guid.NewGuid();
        await _store.GetOrStartOnboardingAsync(memberlessKey, "ghost", "Ghost");
        await _store.CompleteOnboardingAsync(memberlessKey);

        // The member manager finds nobody called "ghost" (see Resolver): the profile row is enough.
        var result = await Controller(isAdmin: true, editAs: "ghost").UpdateBio(new UpdateBioRequest("Edited without a member"), default);

        result.Should().BeOfType<OkObjectResult>();
        (await _store.GetByMemberKeyAsync(memberlessKey))!.Bio.Should().Be("Edited without a member");
        (await BioAsync(_admin)).Should().BeNull();
    }

    [Fact]
    public async Task Every_panel_is_open_to_a_profile_admin_editing_someone_and_unchanged_for_yourself()
    {
        await ProfilesAsync();
        var resolver = Resolver(isAdmin: true);
        var access = new ProfileAccessService(
            Mock.Of<IMemberManager>(m => m.IsLoggedIn() == true && m.GetCurrentMemberAsync() == Task.FromResult<MemberIdentityUser?>(_admin)),
            Mock.Of<IProfileDataProvider>(),
            NullLogger<ProfileAccessService>.Instance);

        var editAs = (await resolver.ResolveAsync(TargetHandle)).Subject!;
        var self = ProfileEditSubject.Self(_admin);

        (await access.CanChooseProfileDisplayAsync(editAs)).Should().BeTrue();
        (await access.CanAddActivityAsync(editAs)).Should().BeTrue();
        (await access.CanMatchLinksAsync(editAs)).Should().BeTrue();
        (await access.CanChooseProfileDisplayAsync(self)).Should().BeFalse();
    }

    [Fact]
    public async Task Naming_yourself_is_not_an_edit_as_request()
    {
        await ProfilesAsync();

        var result = await Resolver(isAdmin: false).ResolveAsync("OCTOCAT");

        result.Subject!.IsEditAs.Should().BeFalse();
        result.Subject.MemberKey.Should().Be(_admin.Key);
        result.Subject.Handle.Should().Be(AdminHandle);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Self_only_actions_refuse_a_request_naming_someone_else_even_from_a_profile_admin(bool isAdmin)
    {
        await ProfilesAsync();

        var start = await Controller(isAdmin, TargetHandle).Start(default);
        var complete = await Controller(isAdmin, TargetHandle).Complete(default);

        start.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        complete.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task A_self_only_action_refuses_a_request_naming_another_member()
    {
        await ProfilesAsync();
        var context = new DefaultHttpContext();
        context.Request.Headers[ProfileEditSubjectResolver.HeaderName] = TargetHandle;

        var result = await Resolver(isAdmin: true).ResolveSelfOnlyAsync(context.Request);

        result.Failure.Should().Be(ProfileEditSubjectFailure.NotAllowed);
        result.FailureResult(ProfileEditSubjectResult.SelfOnlyError).Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task An_edit_as_change_writes_one_audit_line_with_keys_only()
    {
        await ProfilesAsync();
        var resolver = Resolver(isAdmin: true);
        var subject = (await resolver.ResolveAsync(TargetHandle)).Subject!;

        resolver.LogEditAs(subject, "ProfileApi.UpdateBio");

        var line = _log.Entries.Single(e => e.Level == LogLevel.Information).Message;
        line.Should().Contain(_admin.Key.ToString()).And.Contain(_target.Key.ToString()).And.Contain("UpdateBio");
        line.Should().NotContain(TargetHandle).And.NotContain("@");
    }

    [Fact]
    public async Task A_change_to_your_own_profile_is_not_audited()
    {
        await ProfilesAsync();
        var resolver = Resolver(isAdmin: true);
        var subject = (await resolver.ResolveAsync(null)).Subject!;

        resolver.LogEditAs(subject, "ProfileApi.UpdateBio");

        _log.Entries.Should().NotContain(e => e.Level == LogLevel.Information);
    }

    [Fact]
    public void The_account_page_in_edit_as_mode_has_no_email_and_no_self_only_sections()
    {
        var model = new AccountPageViewModel(Page())
        {
            Email = "target@example.com",
            MvpBadgeYear = 2026,
            MvpBadgeDownloadUrl = "/account/badges/umbraco-mvp-2026.png",
            MvpBadgeImageUrl = "/badges/mvp/2026.png",
        };

        AccountPageEditAs.Apply(model, new ProfileEditSubject(_target.Key, TargetHandle, _admin));

        model.IsEditAs.Should().BeTrue();
        model.EditAsHandle.Should().Be(TargetHandle);
        model.Email.Should().BeNull();
        model.MvpBadgeYear.Should().BeNull();
        model.MvpBadgeDownloadUrl.Should().BeNull();
    }

    // The member profile APIs: whose profile they act on comes from the resolver, never straight from the member manager.
    private static readonly Type[] MemberApiControllers =
    [
        typeof(ProfileApiController),
        typeof(ProfileDisplayApiController),
        typeof(MemberFeedsApiController),
        typeof(ProfileActivityEntryApiController),
        typeof(ProfileLinkMatchingApiController),
    ];

    // Actions that only ever act on the signed-in member, and refuse a request naming anyone else.
    private static readonly string[] SelfOnlyActions =
    [
        $"{nameof(ProfileApiController)}.{nameof(ProfileApiController.Start)}",
        $"{nameof(ProfileApiController)}.{nameof(ProfileApiController.Complete)}",
    ];

    [Fact]
    public void Every_member_profile_api_controller_is_listed()
    {
        var found = typeof(ProfileApiController).Assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .Where(t => t.GetCustomAttribute<RouteAttribute>()?.Template is { } route
                && (route.StartsWith("api/profile", StringComparison.OrdinalIgnoreCase) || route.StartsWith("api/member-feeds", StringComparison.OrdinalIgnoreCase)));

        found.Should().BeEquivalentTo(MemberApiControllers);
    }

    [Theory]
    [MemberData(nameof(Controllers))]
    public void No_member_profile_api_resolves_the_current_member_directly(Type controller)
    {
        // Without the member manager, the only way to the member is the resolver (edit-as aware, or self-only).
        controller.GetConstructors().SelectMany(c => c.GetParameters())
            .Should().NotContain(p => p.ParameterType == typeof(IMemberManager))
            .And.Contain(p => p.ParameterType == typeof(ProfileEditSubjectResolver));
    }

    [Theory]
    [MemberData(nameof(Controllers))]
    public void Edit_as_changes_are_audited_except_where_the_api_is_self_only(Type controller)
    {
        var actions = controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(m => $"{controller.Name}.{m.Name}")
            .ToList();
        var allSelfOnly = actions.All(SelfOnlyActions.Contains);

        (controller.GetCustomAttribute<ProfileEditAuditAttribute>() != null || allSelfOnly).Should().BeTrue();
    }

    public static TheoryData<Type> Controllers() => [.. MemberApiControllers];

    private static IPublishedContent Page()
    {
        var contentType = new Mock<IPublishedContentType>();
        contentType.Setup(t => t.Alias).Returns("accountPage");
        var page = new Mock<IPublishedContent>();
        page.Setup(p => p.ContentType).Returns(contentType.Object);
        page.Setup(p => p.Name).Returns("Account");
        return page.Object;
    }
}

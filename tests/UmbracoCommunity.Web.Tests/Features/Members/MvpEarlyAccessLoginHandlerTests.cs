using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.OperationStatus;
using UmbracoCommunity.Web.Features.Members;
using UmbracoCommunity.Web.Features.Mvp.Infrastructure;
using UmbracoCommunity.Web.Features.Mvp.Models;
using Xunit;

namespace UmbracoCommunity.Web.Tests.Features.Members;

public class MvpEarlyAccessLoginHandlerTests
{
    private static readonly Guid MemberKey = Guid.NewGuid();

    private readonly Mock<IExternalMemberService> _externalMemberService = new();
    private readonly Mock<IMvpDataService> _mvpDataService = new();
    private string[] _roles = [];

    public MvpEarlyAccessLoginHandlerTests()
    {
        _externalMemberService
            .Setup(s => s.GetRolesAsync(MemberKey))
            .ReturnsAsync(() => _roles);
        _externalMemberService
            .Setup(s => s.AssignRolesAsync(MemberKey, It.IsAny<string[]>()))
            .ReturnsAsync(Attempt.SucceedWithStatus<ExternalMemberIdentity?, ExternalMemberOperationStatus>(ExternalMemberOperationStatus.Success, null));
        _mvpDataService.Setup(s => s.GetLatest()).Returns(new MvpYear(2026, [Mvp("Octo Cat", "OctoCat")]));
    }

    [Fact]
    public async Task Current_mvp_is_added_to_early_access_ignoring_handle_case()
    {
        SetMember("octocat");

        await Handle();

        _externalMemberService.Verify(
            s => s.AssignRolesAsync(MemberKey, It.Is<string[]>(r => r.SequenceEqual(new[] { "Early access" }))),
            Times.Once);
    }

    [Fact]
    public async Task Member_who_is_not_a_current_mvp_is_left_alone()
    {
        SetMember("someone-else");

        await Handle();

        _externalMemberService.Verify(s => s.AssignRolesAsync(It.IsAny<Guid>(), It.IsAny<string[]>()), Times.Never);
    }

    [Fact]
    public async Task Mvp_only_in_an_older_year_is_not_added()
    {
        SetMember("oldtimer");
        _mvpDataService.Setup(s => s.GetLatest()).Returns(new MvpYear(2026, [Mvp("Octo Cat", "octocat")]));

        await Handle();

        _externalMemberService.Verify(s => s.AssignRolesAsync(It.IsAny<Guid>(), It.IsAny<string[]>()), Times.Never);
    }

    [Fact]
    public async Task Mvp_already_in_early_access_is_not_assigned_again()
    {
        SetMember("octocat");
        _roles = ["Community Members", "early ACCESS"];

        await Handle();

        _externalMemberService.Verify(s => s.AssignRolesAsync(It.IsAny<Guid>(), It.IsAny<string[]>()), Times.Never);
    }

    [Fact]
    public async Task Content_based_member_is_skipped()
    {
        _externalMemberService.Setup(s => s.GetByKeyAsync(MemberKey)).ReturnsAsync((ExternalMemberIdentity?)null);

        await Handle();

        _externalMemberService.Verify(s => s.AssignRolesAsync(It.IsAny<Guid>(), It.IsAny<string[]>()), Times.Never);
    }

    [Fact]
    public async Task Failure_does_not_throw_so_sign_in_is_never_blocked()
    {
        SetMember("octocat");
        _externalMemberService
            .Setup(s => s.AssignRolesAsync(MemberKey, It.IsAny<string[]>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var act = Handle;

        await act.Should().NotThrowAsync();
    }

    private void SetMember(string userName) =>
        _externalMemberService
            .Setup(s => s.GetByKeyAsync(MemberKey))
            .ReturnsAsync(new ExternalMemberIdentity { Key = MemberKey, UserName = userName });

    private Task Handle() =>
        new MvpEarlyAccessLoginHandler(_externalMemberService.Object, _mvpDataService.Object, NullLogger<MvpEarlyAccessLoginHandler>.Instance)
            .HandleAsync(new MemberLoginSuccessNotification("127.0.0.1", MemberKey), CancellationToken.None);

    private static MvpMember Mvp(string name, string handle) => new(name, false, "", null, handle);
}

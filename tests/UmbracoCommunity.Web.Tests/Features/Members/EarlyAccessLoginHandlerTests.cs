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
using UmbracoCommunity.Web.Features.Profiles;
using UmbracoCommunity.Web.Features.Profiles.Models;
using Xunit;

namespace UmbracoCommunity.Web.Tests.Features.Members;

public class EarlyAccessLoginHandlerTests
{
    private static readonly Guid MemberKey = Guid.NewGuid();

    private readonly Mock<IExternalMemberService> _externalMemberService = new();
    private readonly Mock<IMvpDataService> _mvpDataService = new();
    private readonly Mock<IProfileSyncClient> _syncClient = new();
    private string[] _roles = [];

    public EarlyAccessLoginHandlerTests()
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
    public async Task Teammate_is_added_to_early_access()
    {
        SetMember("hq-person");
        _syncClient
            .Setup(c => c.FindProfileAsync("hq-person", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlatformProfile("p1", []) { IsTeammate = true });

        await Handle();

        _externalMemberService.Verify(
            s => s.AssignRolesAsync(MemberKey, It.Is<string[]>(r => r.SequenceEqual(new[] { "Early access" }))),
            Times.Once);
    }

    [Fact]
    public async Task Member_the_platform_does_not_mark_as_a_teammate_is_left_alone()
    {
        SetMember("someone-else");
        _syncClient
            .Setup(c => c.FindProfileAsync("someone-else", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlatformProfile("p1", []));

        await Handle();

        _externalMemberService.Verify(s => s.AssignRolesAsync(It.IsAny<Guid>(), It.IsAny<string[]>()), Times.Never);
    }

    [Fact]
    public async Task A_failing_platform_neither_adds_the_member_nor_blocks_the_sign_in()
    {
        SetMember("hq-person");
        _syncClient
            .Setup(c => c.FindProfileAsync("hq-person", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TaskCanceledException("simulated timeout"));

        var act = Handle;

        await act.Should().NotThrowAsync();
        _externalMemberService.Verify(s => s.AssignRolesAsync(It.IsAny<Guid>(), It.IsAny<string[]>()), Times.Never);
    }

    [Fact]
    public async Task The_platform_is_not_asked_about_a_current_mvp_or_a_member_already_in_early_access()
    {
        SetMember("octocat");
        await Handle();

        SetMember("hq-person");
        _roles = ["Early access"];
        await Handle();

        _syncClient.Verify(c => c.FindProfileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
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
        new EarlyAccessLoginHandler(_externalMemberService.Object, _mvpDataService.Object, _syncClient.Object, NullLogger<EarlyAccessLoginHandler>.Instance)
            .HandleAsync(new MemberLoginSuccessNotification("127.0.0.1", MemberKey), CancellationToken.None);

    private static MvpMember Mvp(string name, string handle) => new(name, false, "", null, handle);
}

using System.Data;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Scoping;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.OperationStatus;
using UmbracoCommunity.Web.Features.Members.Groups;
using Xunit;

namespace UmbracoCommunity.Web.Tests.Features.Members;

public class ExternalMemberGroupServiceTests
{
    private static readonly Guid MemberKey = Guid.NewGuid();

    private readonly Mock<IExternalMemberService> _externalMemberService = new();
    private readonly Mock<IMemberGroupService> _memberGroupService = new();
    private readonly Mock<ICoreScopeProvider> _scopeProvider = new();
    private readonly Mock<ICoreScope> _scope = new();

    private readonly IMemberGroup _communityMembers = Group("Community Members");
    private readonly IMemberGroup _earlyAccess = Group("Early access");
    private readonly IMemberGroup _moderators = Group("Moderators");

    private List<string> _roles = [];

    public ExternalMemberGroupServiceTests()
    {
        _externalMemberService
            .Setup(s => s.GetByKeyAsync(MemberKey))
            .ReturnsAsync(new ExternalMemberIdentity { Key = MemberKey, UserName = "octocat" });
        _externalMemberService
            .Setup(s => s.GetRolesAsync(MemberKey))
            .ReturnsAsync(() => _roles.ToArray());
        _externalMemberService
            .Setup(s => s.AssignRolesAsync(MemberKey, It.IsAny<string[]>()))
            .Callback<Guid, string[]>((_, names) => _roles.AddRange(names))
            .ReturnsAsync(Attempt.SucceedWithStatus<ExternalMemberIdentity?, ExternalMemberOperationStatus>(ExternalMemberOperationStatus.Success, null));
        _externalMemberService
            .Setup(s => s.RemoveRolesAsync(MemberKey, It.IsAny<string[]>()))
            .Callback<Guid, string[]>((_, names) => _roles.RemoveAll(r => names.Contains(r, StringComparer.OrdinalIgnoreCase)))
            .ReturnsAsync(Attempt.SucceedWithStatus<ExternalMemberIdentity?, ExternalMemberOperationStatus>(ExternalMemberOperationStatus.Success, null));

        var all = new[] { _communityMembers, _earlyAccess, _moderators };
        _memberGroupService.Setup(s => s.GetAllAsync()).ReturnsAsync(all);
        _memberGroupService
            .Setup(s => s.GetAsync(It.IsAny<IEnumerable<Guid>>()))
            .ReturnsAsync((IEnumerable<Guid> keys) => all.Where(g => keys.Contains(g.Key)).ToArray());

        _scopeProvider
            .Setup(p => p.CreateCoreScope(
                It.IsAny<IsolationLevel>(),
                It.IsAny<RepositoryCacheMode>(),
                It.IsAny<IEventDispatcher?>(),
                It.IsAny<IScopedNotificationPublisher?>(),
                It.IsAny<bool?>(),
                It.IsAny<bool>(),
                It.IsAny<bool>()))
            .Returns(_scope.Object);
    }

    private ExternalMemberGroupService CreateService() =>
        new(_externalMemberService.Object, _memberGroupService.Object, _scopeProvider.Object, NullLogger<ExternalMemberGroupService>.Instance);

    private static IMemberGroup Group(string name)
    {
        var group = new Mock<IMemberGroup>();
        group.Setup(g => g.Key).Returns(Guid.NewGuid());
        group.Setup(g => g.Name).Returns(name);
        return group.Object;
    }

    [Fact]
    public async Task Get_returns_null_for_a_member_that_is_not_external_only()
    {
        var result = await CreateService().GetAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task Get_returns_the_groups_with_their_keys()
    {
        _roles = ["Early access", "Community Members"];

        var result = await CreateService().GetAsync(MemberKey);

        result!.UserName.Should().Be("octocat");
        result.Groups.Should().Equal(
            new ExternalMemberGroup(_communityMembers.Key, "Community Members"),
            new ExternalMemberGroup(_earlyAccess.Key, "Early access"));
    }

    [Fact]
    public async Task Set_adds_missing_groups_and_removes_unselected_ones()
    {
        _roles = ["Community Members", "Moderators"];

        var result = await CreateService().SetAsync(MemberKey, [_communityMembers.Key, _earlyAccess.Key], "test");

        result.Status.Should().Be(ExternalMemberGroupsUpdateStatus.Success);
        result.Added.Should().Equal("Early access");
        result.Removed.Should().Equal("Moderators");
        result.Groups!.Groups.Select(g => g.Name).Should().Equal("Community Members", "Early access");
        _scope.Verify(s => s.Complete(), Times.Once);
    }

    [Fact]
    public async Task Set_with_an_empty_list_removes_all_groups()
    {
        _roles = ["Community Members", "Early access"];

        var result = await CreateService().SetAsync(MemberKey, [], "test");

        result.Status.Should().Be(ExternalMemberGroupsUpdateStatus.Success);
        result.Groups!.Groups.Should().BeEmpty();
        _externalMemberService.Verify(s => s.AssignRolesAsync(It.IsAny<Guid>(), It.IsAny<string[]>()), Times.Never);
    }

    [Fact]
    public async Task Set_does_nothing_when_the_groups_are_unchanged()
    {
        _roles = ["community members"];

        var result = await CreateService().SetAsync(MemberKey, [_communityMembers.Key], "test");

        result.Status.Should().Be(ExternalMemberGroupsUpdateStatus.Success);
        result.Added.Should().BeEmpty();
        result.Removed.Should().BeEmpty();
        _externalMemberService.Verify(s => s.AssignRolesAsync(It.IsAny<Guid>(), It.IsAny<string[]>()), Times.Never);
        _externalMemberService.Verify(s => s.RemoveRolesAsync(It.IsAny<Guid>(), It.IsAny<string[]>()), Times.Never);
    }

    [Fact]
    public async Task Set_rejects_unknown_group_keys_without_changing_anything()
    {
        _roles = ["Community Members"];
        var unknown = Guid.NewGuid();

        var result = await CreateService().SetAsync(MemberKey, [_earlyAccess.Key, unknown], "test");

        result.Status.Should().Be(ExternalMemberGroupsUpdateStatus.UnknownGroups);
        result.UnknownGroupKeys.Should().Equal(unknown);
        _roles.Should().Equal("Community Members");
        _externalMemberService.Verify(s => s.AssignRolesAsync(It.IsAny<Guid>(), It.IsAny<string[]>()), Times.Never);
        _externalMemberService.Verify(s => s.RemoveRolesAsync(It.IsAny<Guid>(), It.IsAny<string[]>()), Times.Never);
    }

    [Fact]
    public async Task Set_returns_not_found_for_a_member_that_is_not_external_only()
    {
        var result = await CreateService().SetAsync(Guid.NewGuid(), [_earlyAccess.Key], "test");

        result.Status.Should().Be(ExternalMemberGroupsUpdateStatus.MemberNotFound);
        _externalMemberService.Verify(s => s.AssignRolesAsync(It.IsAny<Guid>(), It.IsAny<string[]>()), Times.Never);
    }

    [Fact]
    public async Task Set_does_not_complete_the_scope_when_a_change_fails()
    {
        _roles = ["Moderators"];
        _externalMemberService
            .Setup(s => s.AssignRolesAsync(MemberKey, It.IsAny<string[]>()))
            .ReturnsAsync(Attempt.FailWithStatus<ExternalMemberIdentity?, ExternalMemberOperationStatus>(ExternalMemberOperationStatus.NotFound, null));

        var result = await CreateService().SetAsync(MemberKey, [_earlyAccess.Key], "test");

        result.Status.Should().Be(ExternalMemberGroupsUpdateStatus.Failed);
        _scope.Verify(s => s.Complete(), Times.Never);
        _scope.Verify(s => s.Dispose(), Times.Once);
    }
}

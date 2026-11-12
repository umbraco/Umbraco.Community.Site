using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.OperationStatus;
using UmbracoCommunity.Web.Features.Members.Groups;
using UmbracoCommunity.Web.Features.Profiles;
using Xunit;

namespace UmbracoCommunity.Web.Tests.Features.Members;

public class CreateDevRelMemberGroupMigrationTests
{
    private readonly Mock<IMemberGroupService> _memberGroupService = new();

    public CreateDevRelMemberGroupMigrationTests()
    {
        _memberGroupService
            .Setup(s => s.CreateAsync(It.IsAny<IMemberGroup>()))
            .ReturnsAsync((IMemberGroup group) => Attempt.SucceedWithStatus<IMemberGroup?, MemberGroupOperationStatus>(MemberGroupOperationStatus.Success, group));
    }

    [Fact]
    public async Task Creates_the_DevRel_group_when_it_is_missing()
    {
        GivenGroups("Community Members", ProfileAccessService.EarlyAccessGroupName);

        await RunAsync();

        _memberGroupService.Verify(
            s => s.CreateAsync(It.Is<IMemberGroup>(g => g.Name == ProfileAccessService.DevRelGroupName)),
            Times.Once);
    }

    [Theory]
    [InlineData("DevRel")]
    [InlineData("devrel")]
    [InlineData("DEVREL")]
    public async Task Leaves_an_existing_group_untouched_whatever_its_casing(string existingName)
    {
        GivenGroups("Community Members", existingName);

        await RunAsync();

        _memberGroupService.Verify(s => s.CreateAsync(It.IsAny<IMemberGroup>()), Times.Never);
        _memberGroupService.Verify(s => s.UpdateAsync(It.IsAny<IMemberGroup>()), Times.Never);
        _memberGroupService.Verify(s => s.DeleteAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task Does_not_create_the_early_access_group()
    {
        GivenGroups();

        await RunAsync();

        _memberGroupService.Verify(
            s => s.CreateAsync(It.Is<IMemberGroup>(g => g.Name == ProfileAccessService.EarlyAccessGroupName)),
            Times.Never);
    }

    [Fact]
    public async Task A_failed_create_does_not_throw()
    {
        GivenGroups();
        _memberGroupService
            .Setup(s => s.CreateAsync(It.IsAny<IMemberGroup>()))
            .ReturnsAsync(Attempt.FailWithStatus<IMemberGroup?, MemberGroupOperationStatus>(MemberGroupOperationStatus.DuplicateName, null));

        var run = RunAsync;

        await run.Should().NotThrowAsync();
    }

    private void GivenGroups(params string[] names) =>
        _memberGroupService
            .Setup(s => s.GetAllAsync())
            .ReturnsAsync(names.Select(name => (IMemberGroup)new MemberGroup { Name = name }).ToArray());

    private Task RunAsync() =>
        CreateDevRelMemberGroupMigration.EnsureGroupExistsAsync(_memberGroupService.Object, NullLogger.Instance);
}

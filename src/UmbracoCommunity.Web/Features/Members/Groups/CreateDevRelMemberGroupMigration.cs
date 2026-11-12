using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Migrations;
using UmbracoCommunity.Web.Features.Profiles;

namespace UmbracoCommunity.Web.Features.Members.Groups;

/// <summary>
/// Creates the <see cref="ProfileAccessService.DevRelGroupName"/> member group when it does not exist. A group with
/// that name in any casing (for example one an editor created by hand) is left as it is.
/// </summary>
public sealed class CreateDevRelMemberGroupMigration : AsyncMigrationBase
{
    private readonly IMemberGroupService _memberGroupService;
    private readonly ILogger<CreateDevRelMemberGroupMigration> _logger;

    public CreateDevRelMemberGroupMigration(
        IMigrationContext context,
        IMemberGroupService memberGroupService,
        ILogger<CreateDevRelMemberGroupMigration> logger)
        : base(context)
    {
        _memberGroupService = memberGroupService;
        _logger = logger;
    }

    protected override Task MigrateAsync() => EnsureGroupExistsAsync(_memberGroupService, _logger);

    /// <summary>Creates the DevRel member group unless a group with that name exists, ignoring case.</summary>
    internal static async Task EnsureGroupExistsAsync(IMemberGroupService memberGroupService, ILogger logger)
    {
        var groups = await memberGroupService.GetAllAsync();
        if (groups.Any(g => string.Equals(g.Name, ProfileAccessService.DevRelGroupName, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        var result = await memberGroupService.CreateAsync(new MemberGroup { Name = ProfileAccessService.DevRelGroupName });
        if (!result.Success)
        {
            logger.LogWarning("Could not create the {GroupName} member group: {Status}", ProfileAccessService.DevRelGroupName, result.Status);
        }
    }
}

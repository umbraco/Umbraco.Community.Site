using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Infrastructure.Migrations;
using UmbracoCommunity.ProfileMatchReview.Api;

namespace UmbracoCommunity.ProfileMatchReview.Migrations;

/// <summary>
/// Creates the "Profile match review" user group, with access to the Members section, when it does not exist.
/// An existing group (for example one an editor created by hand) is left as it is.
/// </summary>
public sealed class CreateReviewerUserGroupMigration : AsyncMigrationBase
{
    private readonly IUserGroupService _userGroupService;
    private readonly IShortStringHelper _shortStringHelper;
    private readonly ILogger<CreateReviewerUserGroupMigration> _logger;

    public CreateReviewerUserGroupMigration(
        IMigrationContext context,
        IUserGroupService userGroupService,
        IShortStringHelper shortStringHelper,
        ILogger<CreateReviewerUserGroupMigration> logger)
        : base(context)
    {
        _userGroupService = userGroupService;
        _shortStringHelper = shortStringHelper;
        _logger = logger;
    }

    protected override async Task MigrateAsync()
    {
        if (await _userGroupService.GetAsync(ProfileMatchReviewApiConstants.ReviewerGroupAlias) != null)
        {
            return;
        }

        var group = new UserGroup(_shortStringHelper)
        {
            Alias = ProfileMatchReviewApiConstants.ReviewerGroupAlias,
            Name = "Profile match review",
            Icon = "icon-users",
        };
        group.AddAllowedSection(Constants.Applications.Members);

        var result = await _userGroupService.CreateAsync(group, Constants.Security.SuperUserKey, []);
        if (!result.Success)
        {
            _logger.LogWarning("Could not create the profile match review user group: {Status}", result.Status);
        }
    }
}

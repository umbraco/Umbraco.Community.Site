using Umbraco.Cms.Core.Packaging;

namespace UmbracoCommunity.ProfileMatchReview.Migrations;

/// <summary>
/// Runs on start-up in every environment (Umbraco tracks which steps have run), so the reviewer user group
/// exists after a deploy without anyone creating it by hand.
/// </summary>
public sealed class ProfileMatchReviewMigrationPlan : PackageMigrationPlan
{
    public ProfileMatchReviewMigrationPlan()
        : base("UmbracoCommunity.ProfileMatchReview")
    {
    }

    protected override void DefinePlan()
    {
        To<CreateReviewerUserGroupMigration>("4e0d2d57-6a0b-4c58-9d0f-5f2f8c1f3a11");
    }
}

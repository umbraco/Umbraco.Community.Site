using Umbraco.Cms.Core.Packaging;

namespace UmbracoCommunity.Web.Features.Members.Groups;

/// <summary>
/// Runs on start-up in every environment (Umbraco tracks which steps have run), so the DevRel member group exists
/// after a deploy without anyone creating it by hand.
/// </summary>
public sealed class DevRelMemberGroupMigrationPlan : PackageMigrationPlan
{
    public DevRelMemberGroupMigrationPlan()
        : base("UmbracoCommunity.DevRelMemberGroup")
    {
    }

    protected override void DefinePlan()
    {
        To<CreateDevRelMemberGroupMigration>("243d96cc-394d-4a77-85f5-b600d58e639b");
    }
}

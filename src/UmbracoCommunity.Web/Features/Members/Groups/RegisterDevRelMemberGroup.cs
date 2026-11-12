using Umbraco.Cms.Core.Composing;

namespace UmbracoCommunity.Web.Features.Members.Groups;

/// <summary>Registers the migration plan that creates the DevRel member group on start-up.</summary>
public sealed class RegisterDevRelMemberGroup : IComposer
{
    public void Compose(IUmbracoBuilder builder) =>
        builder.PackageMigrationPlans().Add<DevRelMemberGroupMigrationPlan>();
}

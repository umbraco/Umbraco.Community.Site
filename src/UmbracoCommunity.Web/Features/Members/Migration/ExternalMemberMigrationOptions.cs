namespace UmbracoCommunity.Web.Features.Members.Migration;

/// <summary>
/// Configuration for the content-member to external-member migration, bound to the
/// <c>ExternalMemberMigration</c> section.
/// </summary>
public sealed class ExternalMemberMigrationOptions
{
    public const string SectionName = "ExternalMemberMigration";

    /// <summary>
    /// When true (the default), <see cref="ExternalMemberMigrationStartupJob"/> converts every eligible
    /// content-based member shortly after the application starts. Safe to leave on (a run with nothing
    /// left to convert is a no-op); switch it off once every environment has been migrated.
    /// </summary>
    public bool RunOnStartup { get; set; } = true;
}

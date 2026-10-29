namespace UmbracoCommunity.Web.Features.Members.Migration;

/// <summary>The result of a dry run or a real run of <see cref="ExternalMemberMigrationService"/>.</summary>
/// <param name="DryRun">True when nothing was changed.</param>
/// <param name="Items">One line per content-based community member that was looked at.</param>
public sealed record ExternalMemberMigrationReport(bool DryRun, IReadOnlyList<ExternalMemberMigrationItem> Items)
{
    public int WouldMigrate => Count(ExternalMemberMigrationOutcome.WouldMigrate);

    public int Migrated => Count(ExternalMemberMigrationOutcome.Migrated);

    public int Skipped => Count(ExternalMemberMigrationOutcome.Skipped);

    public int Failed => Count(ExternalMemberMigrationOutcome.Failed);

    private int Count(ExternalMemberMigrationOutcome outcome) => Items.Count(i => i.Outcome == outcome);
}

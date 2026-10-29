namespace UmbracoCommunity.Web.Features.Members.Migration;

/// <summary>What happened (or, in a dry run, would happen) to one content-based member.</summary>
public enum ExternalMemberMigrationOutcome
{
    /// <summary>Dry run: the member passes every check and would be converted.</summary>
    WouldMigrate,

    /// <summary>The member was converted to an external-only member with the same key.</summary>
    Migrated,

    /// <summary>The member was left untouched; <see cref="ExternalMemberMigrationItem.Reason"/> says why.</summary>
    Skipped,

    /// <summary>
    /// The conversion failed. If the content member was already deleted, the reason says so and
    /// the snapshot needed to repair it is in the log.
    /// </summary>
    Failed,
}

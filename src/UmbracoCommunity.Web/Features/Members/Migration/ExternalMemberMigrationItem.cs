namespace UmbracoCommunity.Web.Features.Members.Migration;

/// <summary>One member's line in an <see cref="ExternalMemberMigrationReport"/>.</summary>
/// <param name="Key">The member key. Preserved by the migration, so MemberProfileStore rows keep pointing at it.</param>
/// <param name="UserName">The member's username (the GitHub handle).</param>
/// <param name="Groups">The member groups that are (or would be) carried over.</param>
/// <param name="LoginProviders">The external login providers linked to the member.</param>
/// <param name="Outcome">What happened, or would happen in a dry run.</param>
/// <param name="Reason">Why the member was skipped or failed; null otherwise.</param>
public sealed record ExternalMemberMigrationItem(
    Guid Key,
    string UserName,
    IReadOnlyList<string> Groups,
    IReadOnlyList<string> LoginProviders,
    ExternalMemberMigrationOutcome Outcome,
    string? Reason = null);

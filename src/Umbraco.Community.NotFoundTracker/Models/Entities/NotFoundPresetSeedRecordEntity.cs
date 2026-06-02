namespace Umbraco.Community.NotFoundTracker.Models.Entities;

/// <summary>
/// Tracks every auto-preset rule that has ever been seeded. Persists across editor
/// hard-deletes of the live rule, allowing the seeding service to honour "insert-if-missing"
/// semantics: once a preset path has been seeded, it is never re-inserted even if the editor
/// deleted the corresponding <see cref="NotFoundIgnoreRuleEntity"/> row.
/// </summary>
public class NotFoundPresetSeedRecordEntity
{
    public int Id { get; set; }
    public string? Hostname { get; set; }
    public IgnoreMatchType MatchType { get; set; }
    public string Path { get; set; } = string.Empty;
    public DateTime SeededUtc { get; set; }
}

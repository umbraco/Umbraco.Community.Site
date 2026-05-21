namespace Umbraco.Community.NotFoundTracker.Models.Entities;

public class NotFoundIgnoreRuleEntity
{
    public int Id { get; set; }
    public string? Hostname { get; set; }
    public IgnoreMatchType MatchType { get; set; }
    public string Path { get; set; } = string.Empty;
    public IgnoreRuleSource Source { get; set; } = IgnoreRuleSource.UserDefined;
    public string? Note { get; set; }
    public DateTime CreatedUtc { get; set; }
}

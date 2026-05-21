namespace Umbraco.Community.NotFoundTracker.Models.Api;

public sealed class IgnoreRuleItem
{
    public int Id { get; set; }
    public string? Hostname { get; set; }
    public byte MatchType { get; set; }
    public string Path { get; set; } = string.Empty;
    public byte Source { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedUtc { get; set; }
    public bool IsReadOnly { get; set; }
}

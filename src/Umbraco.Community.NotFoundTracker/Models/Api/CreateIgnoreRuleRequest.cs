namespace Umbraco.Community.NotFoundTracker.Models.Api;

public sealed class CreateIgnoreRuleRequest
{
    public string Path { get; set; } = string.Empty;
    public byte MatchType { get; set; }
    public string? Hostname { get; set; }
    public string? Note { get; set; }
}

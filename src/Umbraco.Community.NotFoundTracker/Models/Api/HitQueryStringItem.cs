namespace Umbraco.Community.NotFoundTracker.Models.Api;

public sealed class HitQueryStringItem
{
    public string QueryString { get; set; } = string.Empty;
    public long HitCount { get; set; }
    public DateTime LastSeenUtc { get; set; }
}

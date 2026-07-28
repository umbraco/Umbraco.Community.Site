namespace Umbraco.Community.NotFoundTracker.Models.Api;

public sealed class HitDetail
{
    public int Id { get; set; }
    public string Hostname { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public long HitCount { get; set; }
    public DateTime FirstSeenUtc { get; set; }
    public DateTime LastSeenUtc { get; set; }
    public string? LastUserAgent { get; set; }
    public byte Status { get; set; }
    public IReadOnlyList<HitQueryStringItem> QueryStrings { get; set; } = Array.Empty<HitQueryStringItem>();
}

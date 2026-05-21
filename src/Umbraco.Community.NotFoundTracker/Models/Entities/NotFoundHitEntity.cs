namespace Umbraco.Community.NotFoundTracker.Models.Entities;

public class NotFoundHitEntity
{
    public int Id { get; set; }
    public string Hostname { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public long HitCount { get; set; }
    public DateTime FirstSeenUtc { get; set; }
    public DateTime LastSeenUtc { get; set; }
    public string? LastUserAgent { get; set; }
    public HitStatus Status { get; set; } = HitStatus.Active;

    public List<NotFoundHitQueryStringEntity> QueryStrings { get; set; } = new();
}

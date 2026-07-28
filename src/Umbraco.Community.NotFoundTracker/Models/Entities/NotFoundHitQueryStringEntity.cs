namespace Umbraco.Community.NotFoundTracker.Models.Entities;

public class NotFoundHitQueryStringEntity
{
    public int Id { get; set; }
    public int HitId { get; set; }
    public string QueryString { get; set; } = string.Empty;
    public long HitCount { get; set; }
    public DateTime LastSeenUtc { get; set; }

    public NotFoundHitEntity? Hit { get; set; }
}

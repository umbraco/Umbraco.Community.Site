namespace Umbraco.Community.NotFoundTracker.Models.Api;

public sealed class HitListResponse
{
    public int Total { get; set; }
    public IReadOnlyList<HitListItem> Items { get; set; } = Array.Empty<HitListItem>();
}

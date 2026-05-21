namespace Umbraco.Community.NotFoundTracker.Models.Api;

public sealed class BulkIgnoreRequest
{
    public IReadOnlyList<int> Ids { get; set; } = Array.Empty<int>();
    public byte MatchType { get; set; }
}

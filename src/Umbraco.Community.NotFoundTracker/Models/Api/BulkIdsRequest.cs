namespace Umbraco.Community.NotFoundTracker.Models.Api;

public sealed class BulkIdsRequest
{
    public IReadOnlyList<int> Ids { get; set; } = Array.Empty<int>();
}

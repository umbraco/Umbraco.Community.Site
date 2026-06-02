namespace Umbraco.Community.NotFoundTracker.Models.Api;

public sealed class BulkOpResponse
{
    public int Processed { get; set; }
    public int Skipped { get; set; }
}

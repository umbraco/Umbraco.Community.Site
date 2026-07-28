namespace Umbraco.Community.NotFoundTracker.Models.Api;

public sealed class CreateRedirectRequest
{
    public Guid TargetContentKey { get; set; }
    public string? Culture { get; set; }
}

namespace Umbraco.Community.NotFoundTracker.Services;

public interface INotFoundRedirectService
{
    Task<RedirectResult> CreateRedirectForHitAsync(int hitId, Guid targetContentKey, string? culture, UserScope scope, CancellationToken ct);
}

public enum RedirectResultKind
{
    Ok,
    HitNotFound,
    Forbidden,
    TargetContentNotFound,
    TargetContentNotAccessible,
    Failed,
}

public sealed record RedirectResult(RedirectResultKind Kind, string? Reason = null);

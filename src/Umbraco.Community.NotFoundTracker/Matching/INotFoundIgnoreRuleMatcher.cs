namespace Umbraco.Community.NotFoundTracker.Matching;

/// <summary>
/// Checks whether a (hostname, path) pair should be ignored — i.e. not recorded as a 404 hit.
/// Plan 1 ships a no-op implementation (always false). Plan 2 replaces it with a hostname-bucketed
/// hash + trie matcher.
/// </summary>
public interface INotFoundIgnoreRuleMatcher
{
    bool IsIgnored(string hostname, string path);

    /// <summary>Refresh the in-memory snapshot from storage. No-op in Plan 1.</summary>
    Task RefreshAsync(CancellationToken cancellationToken = default);
}

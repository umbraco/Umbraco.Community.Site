namespace Umbraco.Community.NotFoundTracker.Matching;

/// <summary>
/// Default <see cref="INotFoundIgnoreRuleMatcher"/> implementation. Holds an immutable
/// <see cref="IgnoreRuleSnapshot"/> behind a volatile reference; <see cref="RefreshAsync"/>
/// builds a new snapshot from the loader and swaps the reference atomically. Readers on the
/// hot path do a single volatile read + lock-free lookup.
/// </summary>
public sealed class IgnoreRuleMatcher : INotFoundIgnoreRuleMatcher
{
    private readonly IgnoreRuleLoader _loader;
    private volatile IgnoreRuleSnapshot _snapshot = IgnoreRuleSnapshot.Empty;

    public IgnoreRuleMatcher(IgnoreRuleLoader loader)
    {
        _loader = loader;
    }

    public bool IsIgnored(string hostname, string path)
    {
        return _snapshot.IsIgnored(hostname, path);
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var fresh = await _loader.LoadAsync(cancellationToken);
        _snapshot = fresh;
    }
}

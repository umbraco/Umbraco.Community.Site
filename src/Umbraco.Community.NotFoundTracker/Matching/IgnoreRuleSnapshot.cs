namespace Umbraco.Community.NotFoundTracker.Matching;

/// <summary>
/// Immutable view of all ignore rules at one point in time. Used by the matcher as an
/// atomically-swappable snapshot — refresh creates a new instance, the matcher swaps its
/// volatile reference, and readers see either the old or new snapshot consistently.
///
/// Lookup checks the per-hostname bucket (if any) AND the global bucket. A rule with
/// <c>Hostname is null</c> lives in the global bucket; everything else lives in the
/// bucket keyed by lowercased hostname.
/// </summary>
public sealed class IgnoreRuleSnapshot
{
    public HostBucket Global { get; }
    public Dictionary<string, HostBucket> ByHostname { get; }

    public IgnoreRuleSnapshot(HostBucket global, Dictionary<string, HostBucket> byHostname)
    {
        Global = global;
        ByHostname = byHostname;
    }

    public static IgnoreRuleSnapshot Empty { get; } = new(new HostBucket(), new(StringComparer.Ordinal));

    public bool IsIgnored(string hostname, string path)
    {
        if (Global.IsIgnored(path)) return true;
        if (!string.IsNullOrEmpty(hostname)
            && ByHostname.TryGetValue(hostname, out var bucket)
            && bucket.IsIgnored(path))
        {
            return true;
        }
        return false;
    }
}

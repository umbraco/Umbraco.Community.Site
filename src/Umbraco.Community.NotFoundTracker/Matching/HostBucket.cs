namespace Umbraco.Community.NotFoundTracker.Matching;

/// <summary>
/// All ignore rules that apply to one hostname (or to global, when used as the snapshot's
/// global bucket). Holds exact-match paths in a HashSet (O(1) lookup) and prefix paths
/// in a trie (O(URL depth) lookup). Both are populated once at snapshot build time and
/// read-only thereafter.
/// </summary>
public sealed class HostBucket
{
    public HashSet<string> ExactPaths { get; } = new(StringComparer.Ordinal);
    public PrefixTrie PrefixPaths { get; } = new();

    public bool IsIgnored(string path)
    {
        if (ExactPaths.Contains(path)) return true;
        return PrefixPaths.Matches(path);
    }
}

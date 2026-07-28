namespace Umbraco.Community.NotFoundTracker.Matching;

/// <summary>
/// Segment-based prefix trie. Each node represents one URL path segment.
/// A node is "terminal" if a rule ends at that node; reaching a terminal
/// during a walk means the path is matched.
///
/// Built once from a static rule set, then read-only. Lookup is O(URL segment depth),
/// independent of the number of rules in the trie.
/// </summary>
public sealed class PrefixTrie
{
    private readonly Node _root = new();

    public void Add(string path)
    {
        var segments = SplitSegments(path);
        if (segments.Length == 0)
        {
            // Root rule ("/" or empty) — matches everything.
            _root.IsTerminal = true;
            return;
        }

        var node = _root;
        foreach (var segment in segments)
        {
            if (!node.Children.TryGetValue(segment, out var next))
            {
                next = new Node();
                node.Children[segment] = next;
            }
            node = next;
        }
        node.IsTerminal = true;
    }

    public bool Matches(string path)
    {
        if (_root.IsTerminal) return true;

        var segments = SplitSegments(path);
        var node = _root;
        foreach (var segment in segments)
        {
            if (!node.Children.TryGetValue(segment, out var next))
            {
                return false;
            }
            node = next;
            if (node.IsTerminal) return true;
        }
        return false;
    }

    private static string[] SplitSegments(string path)
    {
        // Split on '/' removing empty entries handles leading slash, trailing slash,
        // and the root case "/" → [].
        return path.Split('/', StringSplitOptions.RemoveEmptyEntries);
    }

    private sealed class Node
    {
        public Dictionary<string, Node> Children { get; } = new(StringComparer.Ordinal);
        public bool IsTerminal { get; set; }
    }
}

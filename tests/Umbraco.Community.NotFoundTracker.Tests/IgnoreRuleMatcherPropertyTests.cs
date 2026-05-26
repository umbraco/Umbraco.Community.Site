using Umbraco.Community.NotFoundTracker.Matching;

namespace Umbraco.Community.NotFoundTracker.Tests;

/// <summary>
/// Property-based correctness test. Generates random rule sets + random paths and asserts
/// that the trie+hash matcher returns the same answer as a naïve brute-force reference
/// (Any(rule => exact || segmentPrefix)). Catches trie bugs at edge cases that hand-written
/// examples might miss.
/// </summary>
public class IgnoreRuleMatcherPropertyTests
{
    private static readonly string[] Segments = ["foo", "bar", "baz", "wp-admin", "api", "v1", "v2", "users", "old"];

    [Theory]
    [InlineData(42)]
    [InlineData(99)]
    [InlineData(12345)]
    public void Trie_matcher_agrees_with_brute_force_for_random_rules(int seed)
    {
        var rng = new Random(seed);

        // Generate ~50 random rules.
        var ruleSet = Enumerable.Range(0, 50)
            .Select(_ => new RandomRule(
                Path: RandomPath(rng, maxSegments: 3),
                IsExact: rng.Next(2) == 0))
            .ToList();

        // Build the optimized snapshot.
        var bucket = new HostBucket();
        foreach (var rule in ruleSet)
        {
            if (rule.IsExact) bucket.ExactPaths.Add(rule.Path);
            else bucket.PrefixPaths.Add(rule.Path);
        }

        // Compare 500 random paths against the brute-force reference.
        for (var i = 0; i < 500; i++)
        {
            var path = RandomPath(rng, maxSegments: 5);
            var optimized = bucket.IsIgnored(path);
            var bruteForce = BruteForce(ruleSet, path);
            optimized.Should().Be(bruteForce,
                $"path '{path}' with rules {string.Join(", ", ruleSet.Select(r => $"{(r.IsExact ? "E" : "P")}{r.Path}"))}");
        }
    }

    private static string RandomPath(Random rng, int maxSegments)
    {
        var count = rng.Next(1, maxSegments + 1);
        var segs = Enumerable.Range(0, count).Select(_ => Segments[rng.Next(Segments.Length)]);
        return "/" + string.Join('/', segs);
    }

    private static bool BruteForce(List<RandomRule> rules, string path)
    {
        foreach (var rule in rules)
        {
            if (rule.IsExact)
            {
                if (path == rule.Path) return true;
            }
            else
            {
                // Segment-aware prefix: path == rule OR path starts with rule + "/"
                if (path == rule.Path || path.StartsWith(rule.Path + "/", StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private sealed record RandomRule(string Path, bool IsExact);
}

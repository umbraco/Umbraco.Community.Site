using Umbraco.Community.NotFoundTracker.Matching;

namespace Umbraco.Community.NotFoundTracker.Tests;

public class PrefixTrieTests
{
    [Fact]
    public void Empty_trie_matches_nothing()
    {
        var trie = new PrefixTrie();
        trie.Matches("/anything").Should().BeFalse();
        trie.Matches("/").Should().BeFalse();
    }

    [Fact]
    public void Single_segment_rule_matches_exact_and_descendants()
    {
        var trie = new PrefixTrie();
        trie.Add("/wp-admin");

        trie.Matches("/wp-admin").Should().BeTrue();
        trie.Matches("/wp-admin/login").Should().BeTrue();
        trie.Matches("/wp-admin/sub/path").Should().BeTrue();
    }

    [Fact]
    public void Prefix_rule_does_not_match_non_segment_prefix()
    {
        var trie = new PrefixTrie();
        trie.Add("/wp-admin");

        // /wp-administrator shares the string prefix but is a different segment.
        trie.Matches("/wp-administrator").Should().BeFalse();
        trie.Matches("/wp-admin-old").Should().BeFalse();
    }

    [Fact]
    public void Multi_segment_rule_only_matches_under_that_path()
    {
        var trie = new PrefixTrie();
        trie.Add("/api/v1");

        trie.Matches("/api/v1").Should().BeTrue();
        trie.Matches("/api/v1/users").Should().BeTrue();
        trie.Matches("/api/v2").Should().BeFalse();
        trie.Matches("/api").Should().BeFalse();
    }

    [Fact]
    public void Shorter_rule_short_circuits_longer_match()
    {
        var trie = new PrefixTrie();
        trie.Add("/admin");
        trie.Add("/admin/users");   // redundant — short-circuited by /admin

        trie.Matches("/admin/anything").Should().BeTrue();
    }

    [Fact]
    public void Trailing_slash_on_rule_is_normalized()
    {
        var trie = new PrefixTrie();
        trie.Add("/foo/");

        trie.Matches("/foo").Should().BeTrue();
        trie.Matches("/foo/bar").Should().BeTrue();
    }

    [Fact]
    public void Trailing_slash_on_path_is_normalized()
    {
        var trie = new PrefixTrie();
        trie.Add("/foo");

        trie.Matches("/foo/").Should().BeTrue();
    }

    [Fact]
    public void Root_rule_matches_everything()
    {
        var trie = new PrefixTrie();
        trie.Add("/");

        trie.Matches("/").Should().BeTrue();
        trie.Matches("/anything").Should().BeTrue();
        trie.Matches("/a/b/c").Should().BeTrue();
    }
}

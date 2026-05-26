# Umbraco.Community.NotFoundTracker — Plan 2: Ignore rules + auto-preset

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace Plan 1's `NoOpIgnoreRuleMatcher` with a real hostname-bucketed hash + segment-trie matcher, ship a built-in auto-preset of common scanner paths, and add a hosted service that seeds the auto-preset (insert-if-missing) and reconciles `appsettings.json`-driven rules (insert + delete orphans). After this plan, hitting `/wp-admin` is filtered out of recording at the finder, and editors can curate the rule set in two layers (built-in defaults that stick when deleted, config rules that are authoritative).

**Architecture:** The matcher holds a `volatile` reference to an immutable `IgnoreRuleSnapshot`. The snapshot pre-computes a `HostBucket` per hostname (plus one for global rules), where each bucket is `{ HashSet<string> exact paths, PrefixTrie prefix paths }`. Lookup is O(URL depth) regardless of rule count. Refreshing the snapshot is a single atomic reference swap — readers are lock-free. An `IgnoreRuleLoader` projects DB rows into snapshots; `IgnoreRuleMatcher.RefreshAsync()` calls it and swaps the reference. The seeding service runs once on startup (after migrations), seeds defaults + reconciles config, then refreshes the matcher so the first request sees the fresh rule set.

**Tech Stack:** .NET 10, EF Core 10, xUnit + FluentAssertions. No new package dependencies.

**Reference design spec:** `docs/superpowers/specs/2026-05-16-not-found-tracker-design.md` §4 (Ignore rules) + §7 (Configuration) + §8.1 (Unit tests / matcher).

**Branch parent:** `feat/not-found-tracker` (Plan 1's draft PR). Sits on top until Plan 1 merges to `develop`.

---

## File Structure

### New files in `src/Umbraco.Community.NotFoundTracker/`

```
Matching/
  PrefixTrie.cs                          # segment-based trie used inside HostBucket
  HostBucket.cs                          # { HashSet<string> Exact, PrefixTrie Prefix }
  IgnoreRuleSnapshot.cs                  # immutable; global bucket + per-hostname dict
  IgnoreRuleLoader.cs                    # loads NotFoundIgnoreRuleEntity[] → snapshot
  IgnoreRuleMatcher.cs                   # the real INotFoundIgnoreRuleMatcher; replaces NoOp
Infrastructure/
  DefaultIgnoreRules.cs                  # static list of built-in preset entries
  AutoPresetSeedingService.cs            # IHostedService — seed + reconcile + refresh matcher
  AutoPresetRuleConfigParser.cs          # parses AutoPresetRuleConfig.MatchType (string→enum)
```

### Deleted files in `src/Umbraco.Community.NotFoundTracker/`

```
Matching/NoOpIgnoreRuleMatcher.cs        # replaced by Matching/IgnoreRuleMatcher.cs
```

### Modified files in `src/Umbraco.Community.NotFoundTracker/`

```
NotFoundTrackerBuilderExtensions.cs      # swap matcher registration; add seeding hosted service
```

### New test files in `tests/Umbraco.Community.NotFoundTracker.Tests/`

```
PrefixTrieTests.cs
IgnoreRuleMatcherTests.cs
IgnoreRuleMatcherPropertyTests.cs        # property-based vs. brute-force reference
IgnoreRuleLoaderTests.cs
AutoPresetSeedingServiceTests.cs
```

---

## Task 1: PrefixTrie (TDD)

**Files:**
- Create: `tests/Umbraco.Community.NotFoundTracker.Tests/PrefixTrieTests.cs`
- Create: `src/Umbraco.Community.NotFoundTracker/Matching/PrefixTrie.cs`

- [ ] **Step 1: Write the failing tests**

Create `tests/Umbraco.Community.NotFoundTracker.Tests/PrefixTrieTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run tests — expect compilation failure**

```bash
dotnet test tests/Umbraco.Community.NotFoundTracker.Tests/Umbraco.Community.NotFoundTracker.Tests.csproj
```

Expected: build fails — `PrefixTrie` not defined.

- [ ] **Step 3: Implement `PrefixTrie.cs`**

```csharp
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
```

- [ ] **Step 4: Run tests — expect 8 pass**

```bash
dotnet test tests/Umbraco.Community.NotFoundTracker.Tests/Umbraco.Community.NotFoundTracker.Tests.csproj
```

Expected: PrefixTrie tests pass; total 29 (21 existing + 8 new).

- [ ] **Step 5: Commit**

```bash
git add src/Umbraco.Community.NotFoundTracker/Matching/PrefixTrie.cs tests/Umbraco.Community.NotFoundTracker.Tests/PrefixTrieTests.cs
git commit -m "feat(NotFoundTracker): add PrefixTrie for ignore rule matching"
```

---

## Task 2: HostBucket and IgnoreRuleSnapshot

**Files:**
- Create: `src/Umbraco.Community.NotFoundTracker/Matching/HostBucket.cs`
- Create: `src/Umbraco.Community.NotFoundTracker/Matching/IgnoreRuleSnapshot.cs`

- [ ] **Step 1: Create `HostBucket.cs`**

```csharp
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
```

- [ ] **Step 2: Create `IgnoreRuleSnapshot.cs`**

```csharp
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
```

- [ ] **Step 3: Build**

```bash
dotnet build src/Umbraco.Community.NotFoundTracker/Umbraco.Community.NotFoundTracker.csproj
```

Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/Umbraco.Community.NotFoundTracker/Matching/HostBucket.cs src/Umbraco.Community.NotFoundTracker/Matching/IgnoreRuleSnapshot.cs
git commit -m "feat(NotFoundTracker): add HostBucket and IgnoreRuleSnapshot"
```

---

## Task 3: IgnoreRuleLoader (TDD)

The loader converts a flat collection of DB entities into an `IgnoreRuleSnapshot`. Pulled out into its own class because the matcher only depends on snapshots (easy to unit-test the matcher in isolation), and the loader can be tested separately against EF Core.

**Files:**
- Create: `tests/Umbraco.Community.NotFoundTracker.Tests/IgnoreRuleLoaderTests.cs`
- Create: `src/Umbraco.Community.NotFoundTracker/Matching/IgnoreRuleLoader.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using Microsoft.EntityFrameworkCore;
using Umbraco.Community.NotFoundTracker.Infrastructure;
using Umbraco.Community.NotFoundTracker.Matching;
using Umbraco.Community.NotFoundTracker.Models.Entities;

namespace Umbraco.Community.NotFoundTracker.Tests;

public class IgnoreRuleLoaderTests : IDisposable
{
    private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;
    private readonly DbContextOptions<NotFoundTrackerDbContext> _dbOptions;

    public IgnoreRuleLoaderTests()
    {
        _connection = new Microsoft.Data.Sqlite.SqliteConnection("Filename=:memory:");
        _connection.Open();
        _dbOptions = new DbContextOptionsBuilder<NotFoundTrackerDbContext>().UseSqlite(_connection).Options;
        using var ctx = new NotFoundTrackerDbContext(_dbOptions);
        ctx.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private NotFoundTrackerDbContext Ctx() => new(_dbOptions);

    private IgnoreRuleLoader BuildLoader() => new(new TestFactory(_dbOptions));

    [Fact]
    public async Task Empty_table_produces_empty_snapshot()
    {
        var snapshot = await BuildLoader().LoadAsync(CancellationToken.None);

        snapshot.Global.ExactPaths.Should().BeEmpty();
        snapshot.ByHostname.Should().BeEmpty();
    }

    [Fact]
    public async Task Null_hostname_rules_go_into_Global_bucket()
    {
        using (var ctx = Ctx())
        {
            ctx.NotFoundIgnoreRules.Add(new NotFoundIgnoreRuleEntity
            {
                Hostname = null, MatchType = IgnoreMatchType.Exact, Path = "/wp-login.php",
            });
            ctx.NotFoundIgnoreRules.Add(new NotFoundIgnoreRuleEntity
            {
                Hostname = null, MatchType = IgnoreMatchType.PathPrefix, Path = "/wp-admin",
            });
            await ctx.SaveChangesAsync();
        }

        var snapshot = await BuildLoader().LoadAsync(CancellationToken.None);

        snapshot.Global.ExactPaths.Should().Contain("/wp-login.php");
        snapshot.Global.PrefixPaths.Matches("/wp-admin").Should().BeTrue();
        snapshot.Global.PrefixPaths.Matches("/wp-admin/login").Should().BeTrue();
        snapshot.ByHostname.Should().BeEmpty();
    }

    [Fact]
    public async Task Hostname_scoped_rules_go_into_per_hostname_buckets()
    {
        using (var ctx = Ctx())
        {
            ctx.NotFoundIgnoreRules.Add(new NotFoundIgnoreRuleEntity
            {
                Hostname = "site-a.example", MatchType = IgnoreMatchType.Exact, Path = "/old-page",
            });
            ctx.NotFoundIgnoreRules.Add(new NotFoundIgnoreRuleEntity
            {
                Hostname = "site-b.example", MatchType = IgnoreMatchType.PathPrefix, Path = "/legacy",
            });
            await ctx.SaveChangesAsync();
        }

        var snapshot = await BuildLoader().LoadAsync(CancellationToken.None);

        snapshot.ByHostname.Should().HaveCount(2);
        snapshot.ByHostname["site-a.example"].ExactPaths.Should().Contain("/old-page");
        snapshot.ByHostname["site-b.example"].PrefixPaths.Matches("/legacy/old").Should().BeTrue();
    }

    [Fact]
    public async Task Hostnames_are_keyed_case_insensitively()
    {
        using (var ctx = Ctx())
        {
            ctx.NotFoundIgnoreRules.Add(new NotFoundIgnoreRuleEntity
            {
                Hostname = "Example.COM", MatchType = IgnoreMatchType.Exact, Path = "/foo",
            });
            await ctx.SaveChangesAsync();
        }

        var snapshot = await BuildLoader().LoadAsync(CancellationToken.None);

        // Hostname stored is lowercased on the snapshot side so lookups by lowercased
        // request hostname find the bucket.
        snapshot.ByHostname.Should().ContainKey("example.com");
    }

    private sealed class TestFactory : IDbContextFactory<NotFoundTrackerDbContext>
    {
        private readonly DbContextOptions<NotFoundTrackerDbContext> _options;
        public TestFactory(DbContextOptions<NotFoundTrackerDbContext> options) => _options = options;
        public NotFoundTrackerDbContext CreateDbContext() => new(_options);
    }
}
```

- [ ] **Step 2: Run tests — expect compilation failure**

```bash
dotnet test tests/Umbraco.Community.NotFoundTracker.Tests/Umbraco.Community.NotFoundTracker.Tests.csproj
```

Expected: `IgnoreRuleLoader` not defined.

- [ ] **Step 3: Implement `IgnoreRuleLoader.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using Umbraco.Community.NotFoundTracker.Infrastructure;
using Umbraco.Community.NotFoundTracker.Models.Entities;

namespace Umbraco.Community.NotFoundTracker.Matching;

/// <summary>
/// Reads all <see cref="NotFoundIgnoreRuleEntity"/> rows and projects them into an
/// immutable <see cref="IgnoreRuleSnapshot"/>. Called by <see cref="IgnoreRuleMatcher"/>
/// on every <c>RefreshAsync</c>.
/// </summary>
public sealed class IgnoreRuleLoader
{
    private readonly IDbContextFactory<NotFoundTrackerDbContext> _contextFactory;

    public IgnoreRuleLoader(IDbContextFactory<NotFoundTrackerDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<IgnoreRuleSnapshot> LoadAsync(CancellationToken cancellationToken)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var rules = await context.NotFoundIgnoreRules
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var global = new HostBucket();
        var byHost = new Dictionary<string, HostBucket>(StringComparer.Ordinal);

        foreach (var rule in rules)
        {
            var bucket = ResolveBucket(rule, global, byHost);
            AddToBucket(bucket, rule);
        }

        return new IgnoreRuleSnapshot(global, byHost);
    }

    private static HostBucket ResolveBucket(
        NotFoundIgnoreRuleEntity rule,
        HostBucket global,
        Dictionary<string, HostBucket> byHost)
    {
        if (string.IsNullOrEmpty(rule.Hostname))
        {
            return global;
        }

        // Hostnames are stored as the editor entered them; lookup is case-insensitive,
        // so lowercase the key here. The matcher lowercases the request hostname too
        // (via UrlNormalizer), so the keys agree.
        var key = rule.Hostname.ToLowerInvariant();
        if (!byHost.TryGetValue(key, out var bucket))
        {
            bucket = new HostBucket();
            byHost[key] = bucket;
        }
        return bucket;
    }

    private static void AddToBucket(HostBucket bucket, NotFoundIgnoreRuleEntity rule)
    {
        // Paths in storage are already lowercased + normalized — but defensively
        // lowercase here so any test data with mixed casing still maps consistently.
        var path = rule.Path.ToLowerInvariant();
        switch (rule.MatchType)
        {
            case IgnoreMatchType.Exact:
                bucket.ExactPaths.Add(path);
                break;
            case IgnoreMatchType.PathPrefix:
                bucket.PrefixPaths.Add(path);
                break;
        }
    }
}
```

- [ ] **Step 4: Run tests — expect pass**

```bash
dotnet test tests/Umbraco.Community.NotFoundTracker.Tests/Umbraco.Community.NotFoundTracker.Tests.csproj
```

Expected: 33 total tests pass (29 existing + 4 new).

- [ ] **Step 5: Commit**

```bash
git add src/Umbraco.Community.NotFoundTracker/Matching/IgnoreRuleLoader.cs tests/Umbraco.Community.NotFoundTracker.Tests/IgnoreRuleLoaderTests.cs
git commit -m "feat(NotFoundTracker): add IgnoreRuleLoader with tests"
```

---

## Task 4: IgnoreRuleMatcher (TDD)

The real matcher implementation that replaces `NoOpIgnoreRuleMatcher` in DI.

**Files:**
- Create: `tests/Umbraco.Community.NotFoundTracker.Tests/IgnoreRuleMatcherTests.cs`
- Create: `src/Umbraco.Community.NotFoundTracker/Matching/IgnoreRuleMatcher.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using Microsoft.EntityFrameworkCore;
using Umbraco.Community.NotFoundTracker.Infrastructure;
using Umbraco.Community.NotFoundTracker.Matching;
using Umbraco.Community.NotFoundTracker.Models.Entities;

namespace Umbraco.Community.NotFoundTracker.Tests;

public class IgnoreRuleMatcherTests : IDisposable
{
    private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;
    private readonly DbContextOptions<NotFoundTrackerDbContext> _dbOptions;

    public IgnoreRuleMatcherTests()
    {
        _connection = new Microsoft.Data.Sqlite.SqliteConnection("Filename=:memory:");
        _connection.Open();
        _dbOptions = new DbContextOptionsBuilder<NotFoundTrackerDbContext>().UseSqlite(_connection).Options;
        using var ctx = new NotFoundTrackerDbContext(_dbOptions);
        ctx.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private NotFoundTrackerDbContext Ctx() => new(_dbOptions);

    private IgnoreRuleMatcher BuildMatcher() => new(new IgnoreRuleLoader(new TestFactory(_dbOptions)));

    [Fact]
    public void Without_refresh_an_empty_matcher_ignores_nothing()
    {
        var matcher = BuildMatcher();
        matcher.IsIgnored("example.com", "/wp-admin").Should().BeFalse();
    }

    [Fact]
    public async Task After_refresh_global_prefix_rule_matches_any_hostname()
    {
        using (var ctx = Ctx())
        {
            ctx.NotFoundIgnoreRules.Add(new NotFoundIgnoreRuleEntity
            {
                Hostname = null, MatchType = IgnoreMatchType.PathPrefix, Path = "/wp-admin",
            });
            await ctx.SaveChangesAsync();
        }
        var matcher = BuildMatcher();
        await matcher.RefreshAsync();

        matcher.IsIgnored("site-a.example", "/wp-admin").Should().BeTrue();
        matcher.IsIgnored("site-b.example", "/wp-admin/login").Should().BeTrue();
        matcher.IsIgnored("any-host", "/legit-page").Should().BeFalse();
    }

    [Fact]
    public async Task Hostname_scoped_rule_only_matches_that_hostname()
    {
        using (var ctx = Ctx())
        {
            ctx.NotFoundIgnoreRules.Add(new NotFoundIgnoreRuleEntity
            {
                Hostname = "site-a.example",
                MatchType = IgnoreMatchType.Exact,
                Path = "/old-page",
            });
            await ctx.SaveChangesAsync();
        }
        var matcher = BuildMatcher();
        await matcher.RefreshAsync();

        matcher.IsIgnored("site-a.example", "/old-page").Should().BeTrue();
        matcher.IsIgnored("site-b.example", "/old-page").Should().BeFalse();
    }

    [Fact]
    public async Task Refresh_swaps_the_snapshot_atomically()
    {
        var matcher = BuildMatcher();
        await matcher.RefreshAsync();
        matcher.IsIgnored("example.com", "/foo").Should().BeFalse();

        using (var ctx = Ctx())
        {
            ctx.NotFoundIgnoreRules.Add(new NotFoundIgnoreRuleEntity
            {
                Hostname = null, MatchType = IgnoreMatchType.Exact, Path = "/foo",
            });
            await ctx.SaveChangesAsync();
        }

        // Without refresh the matcher still sees the old (empty) snapshot.
        matcher.IsIgnored("example.com", "/foo").Should().BeFalse();

        await matcher.RefreshAsync();
        matcher.IsIgnored("example.com", "/foo").Should().BeTrue();
    }

    private sealed class TestFactory : IDbContextFactory<NotFoundTrackerDbContext>
    {
        private readonly DbContextOptions<NotFoundTrackerDbContext> _options;
        public TestFactory(DbContextOptions<NotFoundTrackerDbContext> options) => _options = options;
        public NotFoundTrackerDbContext CreateDbContext() => new(_options);
    }
}
```

- [ ] **Step 2: Run tests — expect compilation failure**

```bash
dotnet test tests/Umbraco.Community.NotFoundTracker.Tests/Umbraco.Community.NotFoundTracker.Tests.csproj
```

Expected: `IgnoreRuleMatcher` not defined.

- [ ] **Step 3: Implement `IgnoreRuleMatcher.cs`**

```csharp
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
```

- [ ] **Step 4: Run tests — expect pass**

```bash
dotnet test tests/Umbraco.Community.NotFoundTracker.Tests/Umbraco.Community.NotFoundTracker.Tests.csproj
```

Expected: 37 total tests pass (33 existing + 4 new).

- [ ] **Step 5: Commit**

```bash
git add src/Umbraco.Community.NotFoundTracker/Matching/IgnoreRuleMatcher.cs tests/Umbraco.Community.NotFoundTracker.Tests/IgnoreRuleMatcherTests.cs
git commit -m "feat(NotFoundTracker): add real IgnoreRuleMatcher with snapshot swap"
```

---

## Task 5: Property-based correctness test

Compares the trie+hash matcher against a brute-force reference implementation across hundreds of random rule sets + paths. Uses a seeded `Random` for reproducibility — no FsCheck dependency.

**Files:**
- Create: `tests/Umbraco.Community.NotFoundTracker.Tests/IgnoreRuleMatcherPropertyTests.cs`

- [ ] **Step 1: Write the test**

```csharp
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
```

- [ ] **Step 2: Run the test — expect pass**

```bash
dotnet test tests/Umbraco.Community.NotFoundTracker.Tests/Umbraco.Community.NotFoundTracker.Tests.csproj --filter "FullyQualifiedName~IgnoreRuleMatcherPropertyTests"
```

Expected: 3 test cases (one per seed) pass; full suite count rises to 40.

- [ ] **Step 3: Commit**

```bash
git add tests/Umbraco.Community.NotFoundTracker.Tests/IgnoreRuleMatcherPropertyTests.cs
git commit -m "test(NotFoundTracker): property-based test of IgnoreRuleMatcher vs. brute-force"
```

---

## Task 6: DefaultIgnoreRules static list

The built-in auto-preset — common scanner paths that don't belong on any Umbraco site.

**Files:**
- Create: `src/Umbraco.Community.NotFoundTracker/Infrastructure/DefaultIgnoreRules.cs`

- [ ] **Step 1: Create the file**

```csharp
using Umbraco.Community.NotFoundTracker.Models.Entities;

namespace Umbraco.Community.NotFoundTracker.Infrastructure;

/// <summary>
/// Static, code-shipped list of common scanner paths to ignore by default.
/// Inserted on first boot with <see cref="IgnoreRuleSource.AutoPreset"/>.
/// Editor deletions persist (never re-seeded for an already-deleted entry).
///
/// Deliberately excludes things editors should see (sitemap.xml, robots.txt,
/// favicon.ico, .well-known/) so a missing-but-expected URL surfaces as a 404
/// in the dashboard rather than being silently dropped.
/// </summary>
public static class DefaultIgnoreRules
{
    public static readonly IReadOnlyList<DefaultIgnoreRule> All = new[]
    {
        // WordPress / PHP CMS probes
        Prefix("/wp-admin"),
        Prefix("/wp-login"),
        Prefix("/wp-content"),
        Prefix("/wp-includes"),
        Prefix("/wp-json"),
        Exact("/xmlrpc.php"),
        Exact("/wlwmanifest.xml"),

        // Config / secret leak probes
        Exact("/.env"),
        Prefix("/.git"),
        Prefix("/.svn"),
        Prefix("/.aws"),

        // PHP / classic admin paths
        Prefix("/phpmyadmin"),
        Prefix("/pma"),
        Prefix("/myadmin"),
        Exact("/adminer.php"),
        Exact("/admin.php"),

        // IIS / .NET legacy probes
        Prefix("/owa"),
        Prefix("/ecp"),
        Prefix("/autodiscover"),
        Exact("/telerik.web.ui.webresource.axd"),
        Exact("/elmah.axd"),
        Exact("/trace.axd"),

        // Other CMS / framework scanners
        Prefix("/drupal"),
        Prefix("/joomla"),
        Prefix("/typo3"),
        Prefix("/magento"),
        Prefix("/bitrix"),
        Prefix("/laravel"),
        Exact("/.htaccess"),
        Exact("/web.config"),

        // Misc nuisance
        Prefix("/cgi-bin"),
        Prefix("/scripts"),
        Prefix("/cgi"),
        Exact("/server-status"),
        Exact("/server-info"),
        Exact("/hnap1"),
        Prefix("/boaform"),
        Exact("/setup.cgi"),
        Exact("/.ds_store"),
        Exact("/thumbs.db"),

        // Noisy bot lookups — keep but editors can delete
        Exact("/ads.txt"),
        Exact("/app-ads.txt"),
        Exact("/security.txt"),
    };

    private static DefaultIgnoreRule Exact(string path) => new(path, IgnoreMatchType.Exact);
    private static DefaultIgnoreRule Prefix(string path) => new(path, IgnoreMatchType.PathPrefix);
}

public sealed record DefaultIgnoreRule(string Path, IgnoreMatchType MatchType);
```

- [ ] **Step 2: Verify build**

```bash
dotnet build src/Umbraco.Community.NotFoundTracker/Umbraco.Community.NotFoundTracker.csproj
```

- [ ] **Step 3: Commit**

```bash
git add src/Umbraco.Community.NotFoundTracker/Infrastructure/DefaultIgnoreRules.cs
git commit -m "feat(NotFoundTracker): add built-in auto-preset ignore rules"
```

---

## Task 7: AutoPresetRuleConfigParser

Parses the `string MatchType` field on `AutoPresetRuleConfig` into the `IgnoreMatchType` enum, with a helpful error for invalid values.

**Files:**
- Create: `tests/Umbraco.Community.NotFoundTracker.Tests/AutoPresetRuleConfigParserTests.cs`
- Create: `src/Umbraco.Community.NotFoundTracker/Infrastructure/AutoPresetRuleConfigParser.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using Umbraco.Community.NotFoundTracker.Configuration;
using Umbraco.Community.NotFoundTracker.Infrastructure;
using Umbraco.Community.NotFoundTracker.Models.Entities;

namespace Umbraco.Community.NotFoundTracker.Tests;

public class AutoPresetRuleConfigParserTests
{
    [Theory]
    [InlineData("Exact", IgnoreMatchType.Exact)]
    [InlineData("exact", IgnoreMatchType.Exact)]
    [InlineData("EXACT", IgnoreMatchType.Exact)]
    [InlineData("PathPrefix", IgnoreMatchType.PathPrefix)]
    [InlineData("pathprefix", IgnoreMatchType.PathPrefix)]
    public void Parses_valid_match_types(string input, IgnoreMatchType expected)
    {
        var cfg = new AutoPresetRuleConfig { Path = "/foo", MatchType = input };

        var parsed = AutoPresetRuleConfigParser.Parse(cfg);

        parsed.MatchType.Should().Be(expected);
        parsed.Path.Should().Be("/foo");
    }

    [Fact]
    public void Throws_on_invalid_match_type_with_actionable_message()
    {
        var cfg = new AutoPresetRuleConfig { Path = "/foo", MatchType = "Regex" };

        var act = () => AutoPresetRuleConfigParser.Parse(cfg);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Regex*Exact*PathPrefix*");
    }

    [Fact]
    public void Throws_on_empty_path()
    {
        var cfg = new AutoPresetRuleConfig { Path = "", MatchType = "Exact" };

        var act = () => AutoPresetRuleConfigParser.Parse(cfg);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Path*");
    }

    [Fact]
    public void Lowercases_path_and_normalizes_hostname()
    {
        var cfg = new AutoPresetRuleConfig { Path = "/FOO", MatchType = "Exact", Hostname = "Example.COM" };

        var parsed = AutoPresetRuleConfigParser.Parse(cfg);

        parsed.Path.Should().Be("/foo");
        parsed.Hostname.Should().Be("example.com");
    }

    [Fact]
    public void Preserves_null_hostname()
    {
        var cfg = new AutoPresetRuleConfig { Path = "/foo", MatchType = "Exact", Hostname = null };

        var parsed = AutoPresetRuleConfigParser.Parse(cfg);

        parsed.Hostname.Should().BeNull();
    }
}
```

- [ ] **Step 2: Run tests — expect failure**

```bash
dotnet test tests/Umbraco.Community.NotFoundTracker.Tests/Umbraco.Community.NotFoundTracker.Tests.csproj --filter "FullyQualifiedName~AutoPresetRuleConfigParserTests"
```

Expected: `AutoPresetRuleConfigParser` not defined.

- [ ] **Step 3: Implement `AutoPresetRuleConfigParser.cs`**

```csharp
using Umbraco.Community.NotFoundTracker.Configuration;
using Umbraco.Community.NotFoundTracker.Matching;
using Umbraco.Community.NotFoundTracker.Models.Entities;

namespace Umbraco.Community.NotFoundTracker.Infrastructure;

/// <summary>
/// Parses an <see cref="AutoPresetRuleConfig"/> (from appsettings.json) into a
/// <see cref="ParsedAutoPresetRule"/> with the typed enum + normalized path/hostname.
/// Bound as <c>string</c> in the options class rather than the enum directly so
/// invalid values produce a clean error from this parser instead of a generic
/// configuration binding failure.
/// </summary>
public static class AutoPresetRuleConfigParser
{
    public static ParsedAutoPresetRule Parse(AutoPresetRuleConfig cfg)
    {
        if (string.IsNullOrEmpty(cfg.Path))
        {
            throw new InvalidOperationException(
                "AutoPresetRuleConfig.Path is required and cannot be empty.");
        }

        if (!Enum.TryParse<IgnoreMatchType>(cfg.MatchType, ignoreCase: true, out var matchType))
        {
            throw new InvalidOperationException(
                $"Invalid AutoPresetRuleConfig.MatchType '{cfg.MatchType}'. Expected one of: Exact, PathPrefix.");
        }

        return new ParsedAutoPresetRule(
            Path: UrlNormalizer.NormalizePath(cfg.Path),
            MatchType: matchType,
            Hostname: string.IsNullOrEmpty(cfg.Hostname) ? null : UrlNormalizer.NormalizeHostname(cfg.Hostname),
            Note: cfg.Note);
    }
}

public sealed record ParsedAutoPresetRule(
    string Path,
    IgnoreMatchType MatchType,
    string? Hostname,
    string? Note);
```

- [ ] **Step 4: Run tests — expect pass**

```bash
dotnet test tests/Umbraco.Community.NotFoundTracker.Tests/Umbraco.Community.NotFoundTracker.Tests.csproj
```

Expected: all tests pass (44 total).

- [ ] **Step 5: Commit**

```bash
git add src/Umbraco.Community.NotFoundTracker/Infrastructure/AutoPresetRuleConfigParser.cs tests/Umbraco.Community.NotFoundTracker.Tests/AutoPresetRuleConfigParserTests.cs
git commit -m "feat(NotFoundTracker): parse AutoPresetRuleConfig with typed enum"
```

---

## Task 8: AutoPresetSeedingService — auto-preset insert-if-missing (TDD)

The hosted service that runs on startup. This task only implements the **built-in `AutoPreset`** branch — insert any preset entry whose `(Hostname, MatchType, Path)` is not already in the table, never remove. Editor deletions stick.

Task 9 adds the `ConfigSeeded` reconcile branch in the same service.

**Files:**
- Create: `tests/Umbraco.Community.NotFoundTracker.Tests/AutoPresetSeedingServiceTests.cs`
- Create: `src/Umbraco.Community.NotFoundTracker/Infrastructure/AutoPresetSeedingService.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Umbraco.Community.NotFoundTracker.Configuration;
using Umbraco.Community.NotFoundTracker.Infrastructure;
using Umbraco.Community.NotFoundTracker.Matching;
using Umbraco.Community.NotFoundTracker.Models.Entities;

namespace Umbraco.Community.NotFoundTracker.Tests;

public class AutoPresetSeedingServiceTests : IDisposable
{
    private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;
    private readonly DbContextOptions<NotFoundTrackerDbContext> _dbOptions;

    public AutoPresetSeedingServiceTests()
    {
        _connection = new Microsoft.Data.Sqlite.SqliteConnection("Filename=:memory:");
        _connection.Open();
        _dbOptions = new DbContextOptionsBuilder<NotFoundTrackerDbContext>().UseSqlite(_connection).Options;
        using var ctx = new NotFoundTrackerDbContext(_dbOptions);
        ctx.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private NotFoundTrackerDbContext Ctx() => new(_dbOptions);

    private (AutoPresetSeedingService service, IgnoreRuleMatcher matcher) Build(NotFoundTrackerOptions opts)
    {
        var factory = new TestFactory(_dbOptions);
        var loader = new IgnoreRuleLoader(factory);
        var matcher = new IgnoreRuleMatcher(loader);
        var service = new AutoPresetSeedingService(
            factory,
            matcher,
            new OptionsWrapper<NotFoundTrackerOptions>(opts),
            NullLogger<AutoPresetSeedingService>.Instance);
        return (service, matcher);
    }

    [Fact]
    public async Task Seeds_all_built_in_preset_entries_when_table_is_empty()
    {
        var (service, _) = Build(new NotFoundTrackerOptions { SeedAutoPreset = true });

        await service.StartAsync(CancellationToken.None);

        using var verify = Ctx();
        var rows = await verify.NotFoundIgnoreRules.ToListAsync();
        rows.Should().HaveCount(DefaultIgnoreRules.All.Count);
        rows.Should().OnlyContain(r => r.Source == IgnoreRuleSource.AutoPreset);
        rows.Should().Contain(r => r.Path == "/wp-admin" && r.MatchType == IgnoreMatchType.PathPrefix);
    }

    [Fact]
    public async Task Does_not_re_insert_already_present_preset_entries()
    {
        // Seed once.
        var (service1, _) = Build(new NotFoundTrackerOptions { SeedAutoPreset = true });
        await service1.StartAsync(CancellationToken.None);

        var initialCount = await Ctx().NotFoundIgnoreRules.CountAsync();

        // Seed again — same defaults, should be no-ops.
        var (service2, _) = Build(new NotFoundTrackerOptions { SeedAutoPreset = true });
        await service2.StartAsync(CancellationToken.None);

        (await Ctx().NotFoundIgnoreRules.CountAsync()).Should().Be(initialCount);
    }

    [Fact]
    public async Task Editor_deleted_AutoPreset_entries_are_not_re_inserted()
    {
        // Seed first run.
        var (service1, _) = Build(new NotFoundTrackerOptions { SeedAutoPreset = true });
        await service1.StartAsync(CancellationToken.None);

        // Editor deletes /wp-admin from the dashboard.
        using (var ctx = Ctx())
        {
            var wpAdmin = await ctx.NotFoundIgnoreRules.FirstAsync(r => r.Path == "/wp-admin");
            ctx.NotFoundIgnoreRules.Remove(wpAdmin);
            await ctx.SaveChangesAsync();
        }

        // Restart: seeding runs again. /wp-admin must stay deleted.
        var (service2, _) = Build(new NotFoundTrackerOptions { SeedAutoPreset = true });
        await service2.StartAsync(CancellationToken.None);

        using var verify = Ctx();
        (await verify.NotFoundIgnoreRules.AnyAsync(r => r.Path == "/wp-admin")).Should().BeFalse();
    }

    [Fact]
    public async Task SeedAutoPreset_false_skips_built_in_preset()
    {
        var (service, _) = Build(new NotFoundTrackerOptions { SeedAutoPreset = false });

        await service.StartAsync(CancellationToken.None);

        (await Ctx().NotFoundIgnoreRules.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Refreshes_matcher_after_seeding()
    {
        var (service, matcher) = Build(new NotFoundTrackerOptions { SeedAutoPreset = true });

        await service.StartAsync(CancellationToken.None);

        // Built-in preset includes /wp-admin as a PathPrefix rule.
        matcher.IsIgnored("any-host", "/wp-admin").Should().BeTrue();
        matcher.IsIgnored("any-host", "/wp-admin/login").Should().BeTrue();
        matcher.IsIgnored("any-host", "/legit").Should().BeFalse();
    }

    private sealed class TestFactory : IDbContextFactory<NotFoundTrackerDbContext>
    {
        private readonly DbContextOptions<NotFoundTrackerDbContext> _options;
        public TestFactory(DbContextOptions<NotFoundTrackerDbContext> options) => _options = options;
        public NotFoundTrackerDbContext CreateDbContext() => new(_options);
    }
}
```

- [ ] **Step 2: Run tests — expect compilation failure**

```bash
dotnet test tests/Umbraco.Community.NotFoundTracker.Tests/Umbraco.Community.NotFoundTracker.Tests.csproj
```

Expected: `AutoPresetSeedingService` not defined.

- [ ] **Step 3: Implement `AutoPresetSeedingService.cs`** (auto-preset branch only — config branch added in Task 9)

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Community.NotFoundTracker.Configuration;
using Umbraco.Community.NotFoundTracker.Matching;
using Umbraco.Community.NotFoundTracker.Models.Entities;

namespace Umbraco.Community.NotFoundTracker.Infrastructure;

/// <summary>
/// Runs once on application startup AFTER migrations. Two responsibilities:
///   1. Insert any built-in <see cref="DefaultIgnoreRules"/> entries that aren't already
///      in the table, tagged <see cref="IgnoreRuleSource.AutoPreset"/>. Insert-if-missing —
///      editor deletions persist (never re-inserted on subsequent restarts).
///   2. (Task 9 of Plan 2 — added later) Reconcile <see cref="IgnoreRuleSource.ConfigSeeded"/>
///      entries against <see cref="NotFoundTrackerOptions.AdditionalAutoPresetRules"/>:
///      insert missing, delete orphans.
///
/// After both branches run, refreshes <see cref="IgnoreRuleMatcher"/> so the first request
/// sees the up-to-date rule set.
/// </summary>
public sealed class AutoPresetSeedingService : IHostedService
{
    private readonly IDbContextFactory<NotFoundTrackerDbContext> _contextFactory;
    private readonly INotFoundIgnoreRuleMatcher _matcher;
    private readonly IOptions<NotFoundTrackerOptions> _options;
    private readonly ILogger<AutoPresetSeedingService> _logger;

    public AutoPresetSeedingService(
        IDbContextFactory<NotFoundTrackerDbContext> contextFactory,
        INotFoundIgnoreRuleMatcher matcher,
        IOptions<NotFoundTrackerOptions> options,
        ILogger<AutoPresetSeedingService> logger)
    {
        _contextFactory = contextFactory;
        _matcher = matcher;
        _options = options;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await SeedAutoPresetAsync(cancellationToken);
            await _matcher.RefreshAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NotFoundTracker auto-preset seeding failed");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task SeedAutoPresetAsync(CancellationToken ct)
    {
        if (!_options.Value.SeedAutoPreset)
        {
            _logger.LogInformation("NotFoundTracker auto-preset seeding skipped (SeedAutoPreset=false)");
            return;
        }

        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        // Load existing rows once to avoid round-trips per preset entry.
        var existing = await context.NotFoundIgnoreRules
            .Select(r => new { r.Hostname, r.MatchType, r.Path })
            .ToListAsync(ct);

        var existingKeys = new HashSet<(string?, IgnoreMatchType, string)>(
            existing.Select(r => (r.Hostname, r.MatchType, r.Path)));

        var inserted = 0;
        foreach (var rule in DefaultIgnoreRules.All)
        {
            var key = ((string?)null, rule.MatchType, rule.Path);
            if (existingKeys.Contains(key)) continue;

            context.NotFoundIgnoreRules.Add(new NotFoundIgnoreRuleEntity
            {
                Hostname = null,
                MatchType = rule.MatchType,
                Path = rule.Path,
                Source = IgnoreRuleSource.AutoPreset,
                CreatedUtc = DateTime.UtcNow,
            });
            inserted++;
        }

        if (inserted > 0)
        {
            await context.SaveChangesAsync(ct);
            _logger.LogInformation("NotFoundTracker seeded {Count} auto-preset rules", inserted);
        }
    }
}
```

- [ ] **Step 4: Run tests — expect pass**

```bash
dotnet test tests/Umbraco.Community.NotFoundTracker.Tests/Umbraco.Community.NotFoundTracker.Tests.csproj
```

Expected: all tests pass (49 total).

- [ ] **Step 5: Commit**

```bash
git add src/Umbraco.Community.NotFoundTracker/Infrastructure/AutoPresetSeedingService.cs tests/Umbraco.Community.NotFoundTracker.Tests/AutoPresetSeedingServiceTests.cs
git commit -m "feat(NotFoundTracker): seed built-in auto-preset on startup"
```

---

## Task 9: AutoPresetSeedingService — ConfigSeeded reconcile (TDD)

Extend the seeding service with the `ConfigSeeded` reconcile branch: insert any rule listed in `AdditionalAutoPresetRules` that isn't already present as `ConfigSeeded`, AND delete any existing `ConfigSeeded` rule no longer listed in config (config is authoritative).

**Files:**
- Modify: `tests/Umbraco.Community.NotFoundTracker.Tests/AutoPresetSeedingServiceTests.cs`
- Modify: `src/Umbraco.Community.NotFoundTracker/Infrastructure/AutoPresetSeedingService.cs`

- [ ] **Step 1: Append new tests to `AutoPresetSeedingServiceTests.cs`**

Add these test methods inside the existing `AutoPresetSeedingServiceTests` class (before the `TestFactory` private class):

```csharp
    [Fact]
    public async Task Config_rules_are_inserted_with_ConfigSeeded_source()
    {
        var opts = new NotFoundTrackerOptions
        {
            SeedAutoPreset = false,  // isolate: only test config branch
            AdditionalAutoPresetRules =
            {
                new AutoPresetRuleConfig { Path = "/legacy", MatchType = "PathPrefix" },
                new AutoPresetRuleConfig { Path = "/secret.html", MatchType = "Exact", Hostname = "site-a.example" },
            }
        };
        var (service, _) = Build(opts);

        await service.StartAsync(CancellationToken.None);

        using var verify = Ctx();
        var rows = await verify.NotFoundIgnoreRules.ToListAsync();
        rows.Should().HaveCount(2);
        rows.Should().OnlyContain(r => r.Source == IgnoreRuleSource.ConfigSeeded);

        rows.Should().Contain(r => r.Path == "/legacy" && r.MatchType == IgnoreMatchType.PathPrefix && r.Hostname == null);
        rows.Should().Contain(r => r.Path == "/secret.html" && r.MatchType == IgnoreMatchType.Exact && r.Hostname == "site-a.example");
    }

    [Fact]
    public async Task Config_rule_removed_from_config_is_deleted_on_next_boot()
    {
        // First boot: config has /legacy.
        var (service1, _) = Build(new NotFoundTrackerOptions
        {
            SeedAutoPreset = false,
            AdditionalAutoPresetRules = { new AutoPresetRuleConfig { Path = "/legacy", MatchType = "PathPrefix" } }
        });
        await service1.StartAsync(CancellationToken.None);

        (await Ctx().NotFoundIgnoreRules.AnyAsync(r => r.Path == "/legacy")).Should().BeTrue();

        // Second boot: /legacy removed from config.
        var (service2, _) = Build(new NotFoundTrackerOptions
        {
            SeedAutoPreset = false,
            AdditionalAutoPresetRules = { }  // empty
        });
        await service2.StartAsync(CancellationToken.None);

        (await Ctx().NotFoundIgnoreRules.AnyAsync(r => r.Path == "/legacy")).Should().BeFalse();
    }

    [Fact]
    public async Task UserDefined_rule_with_same_path_as_config_is_not_clobbered()
    {
        // Editor manually added /shared with Source=UserDefined.
        using (var ctx = Ctx())
        {
            ctx.NotFoundIgnoreRules.Add(new NotFoundIgnoreRuleEntity
            {
                Hostname = null,
                MatchType = IgnoreMatchType.PathPrefix,
                Path = "/shared",
                Source = IgnoreRuleSource.UserDefined,
                CreatedUtc = DateTime.UtcNow,
            });
            await ctx.SaveChangesAsync();
        }

        // Config also declares /shared.
        var (service, _) = Build(new NotFoundTrackerOptions
        {
            SeedAutoPreset = false,
            AdditionalAutoPresetRules =
            {
                new AutoPresetRuleConfig { Path = "/shared", MatchType = "PathPrefix" }
            }
        });
        await service.StartAsync(CancellationToken.None);

        using var verify = Ctx();
        var rows = await verify.NotFoundIgnoreRules.Where(r => r.Path == "/shared").ToListAsync();
        rows.Should().HaveCount(2);
        rows.Should().ContainSingle(r => r.Source == IgnoreRuleSource.UserDefined);
        rows.Should().ContainSingle(r => r.Source == IgnoreRuleSource.ConfigSeeded);
    }

    [Fact]
    public async Task AutoPreset_rule_with_same_path_as_config_remains_separate()
    {
        // First boot: built-in preset seeded /wp-admin. Then config adds /wp-admin too.
        var (service, _) = Build(new NotFoundTrackerOptions
        {
            SeedAutoPreset = true,
            AdditionalAutoPresetRules =
            {
                new AutoPresetRuleConfig { Path = "/wp-admin", MatchType = "PathPrefix" }
            }
        });
        await service.StartAsync(CancellationToken.None);

        using var verify = Ctx();
        var rows = await verify.NotFoundIgnoreRules.Where(r => r.Path == "/wp-admin").ToListAsync();
        rows.Should().HaveCount(2);
        rows.Should().ContainSingle(r => r.Source == IgnoreRuleSource.AutoPreset);
        rows.Should().ContainSingle(r => r.Source == IgnoreRuleSource.ConfigSeeded);
    }

    [Fact]
    public async Task Invalid_match_type_in_config_throws_at_startup()
    {
        var (service, _) = Build(new NotFoundTrackerOptions
        {
            SeedAutoPreset = false,
            AdditionalAutoPresetRules =
            {
                new AutoPresetRuleConfig { Path = "/foo", MatchType = "Regex" }
            }
        });

        var act = async () => await service.StartAsync(CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Regex*Exact*PathPrefix*");
    }
```

- [ ] **Step 2: Run tests — expect failure** (current impl ignores config rules)

```bash
dotnet test tests/Umbraco.Community.NotFoundTracker.Tests/Umbraco.Community.NotFoundTracker.Tests.csproj --filter "FullyQualifiedName~AutoPresetSeedingServiceTests"
```

Expected: 5 new tests fail.

- [ ] **Step 3: Update `AutoPresetSeedingService.cs` — replace `StartAsync` body and add the new `ReconcileConfigSeededAsync` method**

Replace the `StartAsync` method body with:

```csharp
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await SeedAutoPresetAsync(cancellationToken);
            await ReconcileConfigSeededAsync(cancellationToken);
            await _matcher.RefreshAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NotFoundTracker auto-preset seeding failed");
            throw;
        }
    }
```

Then add this new private method below `SeedAutoPresetAsync`:

```csharp
    private async Task ReconcileConfigSeededAsync(CancellationToken ct)
    {
        // Parse all config-declared rules up front so an invalid MatchType fails fast
        // before any DB writes happen.
        var desired = _options.Value.AdditionalAutoPresetRules
            .Select(AutoPresetRuleConfigParser.Parse)
            .Select(r => (r.Hostname, r.MatchType, r.Path))
            .ToHashSet();

        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var existingConfigSeeded = await context.NotFoundIgnoreRules
            .Where(r => r.Source == IgnoreRuleSource.ConfigSeeded)
            .ToListAsync(ct);

        var existingKeys = existingConfigSeeded
            .Select(r => (r.Hostname, r.MatchType, r.Path))
            .ToHashSet();

        // Delete config-seeded rows no longer declared in config.
        var toDelete = existingConfigSeeded
            .Where(r => !desired.Contains((r.Hostname, r.MatchType, r.Path)))
            .ToList();
        if (toDelete.Count > 0)
        {
            context.NotFoundIgnoreRules.RemoveRange(toDelete);
        }

        // Insert config rules not yet present (with Source=ConfigSeeded only — a UserDefined
        // or AutoPreset row with the same path stays separate).
        var toInsert = desired
            .Where(key => !existingKeys.Contains(key))
            .ToList();
        foreach (var (hostname, matchType, path) in toInsert)
        {
            context.NotFoundIgnoreRules.Add(new NotFoundIgnoreRuleEntity
            {
                Hostname = hostname,
                MatchType = matchType,
                Path = path,
                Source = IgnoreRuleSource.ConfigSeeded,
                CreatedUtc = DateTime.UtcNow,
            });
        }

        if (toDelete.Count > 0 || toInsert.Count > 0)
        {
            await context.SaveChangesAsync(ct);
            _logger.LogInformation(
                "NotFoundTracker reconciled config-seeded rules: +{Inserted} -{Deleted}",
                toInsert.Count, toDelete.Count);
        }
    }
```

- [ ] **Step 4: Run tests — expect all pass**

```bash
dotnet test tests/Umbraco.Community.NotFoundTracker.Tests/Umbraco.Community.NotFoundTracker.Tests.csproj
```

Expected: 54 total tests pass (49 existing + 5 new).

- [ ] **Step 5: Commit**

```bash
git add src/Umbraco.Community.NotFoundTracker/Infrastructure/AutoPresetSeedingService.cs tests/Umbraco.Community.NotFoundTracker.Tests/AutoPresetSeedingServiceTests.cs
git commit -m "feat(NotFoundTracker): reconcile config-seeded ignore rules on startup"
```

---

## Task 10: Wire matcher swap + seeding into builder extension

Replace the `NoOpIgnoreRuleMatcher` registration with the real `IgnoreRuleMatcher`, register `IgnoreRuleLoader`, register `AutoPresetSeedingService` as a hosted service, and delete the now-unused `NoOpIgnoreRuleMatcher`.

**Files:**
- Modify: `src/Umbraco.Community.NotFoundTracker/NotFoundTrackerBuilderExtensions.cs`
- Delete: `src/Umbraco.Community.NotFoundTracker/Matching/NoOpIgnoreRuleMatcher.cs`

- [ ] **Step 1: Update the builder extension**

Open `src/Umbraco.Community.NotFoundTracker/NotFoundTrackerBuilderExtensions.cs` and change the matcher registration line. Currently:

```csharp
        builder.Services.TryAddSingleton<INotFoundIgnoreRuleMatcher, NoOpIgnoreRuleMatcher>();
```

Replace with:

```csharp
        builder.Services.AddSingleton<IgnoreRuleLoader>();
        builder.Services.AddSingleton<INotFoundIgnoreRuleMatcher, IgnoreRuleMatcher>();
        builder.Services.AddHostedService<AutoPresetSeedingService>();
```

`AddSingleton` (not `TryAddSingleton`) so this overrides any previously-registered no-op — though after the deletion in Step 2 there isn't one anyway. Explicit is clearer.

Make sure the `using` directive `using Umbraco.Community.NotFoundTracker.Infrastructure;` is present (it should already be — `NotFoundTrackerMigrationHostedService` is in that namespace).

- [ ] **Step 2: Delete `NoOpIgnoreRuleMatcher.cs`**

```bash
rm src/Umbraco.Community.NotFoundTracker/Matching/NoOpIgnoreRuleMatcher.cs
```

- [ ] **Step 3: Verify the solution builds**

```bash
dotnet build UmbracoCommunity.sln
```

Expected: 0 errors. The `NoOpIgnoreRuleMatcher` class wasn't referenced anywhere outside its own file (we used `TryAddSingleton<INotFoundIgnoreRuleMatcher, NoOpIgnoreRuleMatcher>` — the only consumer was DI).

- [ ] **Step 4: Run all tests**

```bash
dotnet test tests/Umbraco.Community.NotFoundTracker.Tests/Umbraco.Community.NotFoundTracker.Tests.csproj
```

Expected: 54 tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/Umbraco.Community.NotFoundTracker/NotFoundTrackerBuilderExtensions.cs src/Umbraco.Community.NotFoundTracker/Matching/NoOpIgnoreRuleMatcher.cs
git commit -m "feat(NotFoundTracker): activate real IgnoreRuleMatcher + auto-preset seeding"
```

---

## Task 11: End-to-end manual verification

Run the dev server, confirm the seeding service runs, confirm the preset is in the DB, confirm `/wp-admin` requests don't create hit rows.

**Steps:**

- [ ] **Step 1: Start the backend in Development mode**

```bash
cd src/UmbracoCommunity.Web.UI && ASPNETCORE_ENVIRONMENT=Development dotnet run --no-launch-profile --urls "http://localhost:65178"
```

Expected log lines on startup (in order):

```
Applying NotFoundTracker database migrations...
... (no pending migrations message, since Plan 1 already ran InitialCreate)
NotFoundTracker seeded N auto-preset rules
```

If you're running on the same DB from Plan 1's verification, the seeding service will insert the preset entries since the table was empty. On a subsequent restart, the log will say nothing about seeding (zero inserted, zero deleted) — that's correct.

- [ ] **Step 2: Verify the preset is in the DB**

```bash
python3 -c "
import sqlite3
conn = sqlite3.connect('src/UmbracoCommunity.Web.UI/umbraco/Data/Umbraco.sqlite.db')
cur = conn.cursor()
cur.execute('SELECT Source, COUNT(*) FROM NotFoundIgnoreRules GROUP BY Source;')
print('By source:', cur.fetchall())
cur.execute('SELECT Hostname, MatchType, Path FROM NotFoundIgnoreRules WHERE Source = 1 LIMIT 5;')
print('Sample AutoPreset rows:', cur.fetchall())
"
```

Expected: a count for `Source=1` (AutoPreset) equal to `DefaultIgnoreRules.All.Count` (~40). Sample rows include `/wp-admin`, `/wp-login`, etc.

- [ ] **Step 3: Hit `/wp-admin` and verify NO hit is recorded**

```bash
curl -s -o /dev/null http://localhost:65178/wp-admin
curl -s -o /dev/null http://localhost:65178/wp-admin/login
```

Wait ~7 seconds for the writer flush window, then:

```bash
python3 -c "
import sqlite3
conn = sqlite3.connect('src/UmbracoCommunity.Web.UI/umbraco/Data/Umbraco.sqlite.db')
cur = conn.cursor()
cur.execute('SELECT Path FROM NotFoundHits WHERE Path LIKE \"/wp-%\";')
print('wp-* hits:', cur.fetchall())
"
```

Expected: empty result. The ignore matcher filtered both URLs at the finder before they hit the channel.

NOTE: This step only proves the ignore behaviour if the host has published content for Umbraco to route normally. If you're on the same content-less local DB from Plan 1's verification, Umbraco's welcome middleware still short-circuits routing and our finder never runs — meaning no hit is recorded either way. Re-test on a content-loaded dev DB to verify the ignore path specifically.

- [ ] **Step 4: Test a config-seeded rule**

Edit `src/UmbracoCommunity.Web.UI/appsettings.Development.json` and add the `NotFoundTracker` section:

```json
  "NotFoundTracker": {
    "AdditionalAutoPresetRules": [
      { "Path": "/local-noise", "MatchType": "PathPrefix" }
    ]
  }
```

Save, restart the server. Verify the rule appears as `ConfigSeeded`:

```bash
python3 -c "
import sqlite3
conn = sqlite3.connect('src/UmbracoCommunity.Web.UI/umbraco/Data/Umbraco.sqlite.db')
cur = conn.cursor()
cur.execute('SELECT Source, Hostname, MatchType, Path FROM NotFoundIgnoreRules WHERE Source = 2;')
print('Config-seeded:', cur.fetchall())
"
```

Expected: `[(2, None, 1, '/local-noise')]` — `Source=2` (ConfigSeeded), `MatchType=1` (PathPrefix).

- [ ] **Step 5: Remove the config rule and verify it's deleted on restart**

Remove the `NotFoundTracker` section from `appsettings.Development.json`. Restart. Re-run the query — expect empty results. (This proves config authority: removing from config removes from DB on next boot.)

- [ ] **Step 6: Stop the server**

`Ctrl+C` or kill the process.

- [ ] **Step 7: No commit** unless something needed amending. If you adjusted `appsettings.Development.json` for testing and reverted, no commit is required.

---

## Task 11 verification status (recorded 2026-05-18)

Partial — the user's own dev server was already running on port 65178, so my own startup attempt couldn't complete its bind. However, the startup ran far enough that **migrations and the seeding service had already fired** before the port bind failed (hosted services run during host startup, before Kestrel binds the listening port). What was confirmed in the live environment:

- ✅ Migration `20260518072106_AddPresetSeedRecords` applied cleanly.
- ✅ Seeding log: `NotFoundTracker seeded 43 auto-preset rules` — matches `DefaultIgnoreRules.All.Count`.
- ✅ `NotFoundIgnoreRules` table: 43 rows, all `Source=1` (AutoPreset).
- ✅ `NotFoundPresetSeedRecords` tombstone table: 43 rows.
- ✅ Sample contents verified: `/wp-admin` (Prefix), `/wp-login` (Prefix), `/xmlrpc.php` (Exact), etc.

Plan deviation: Task 8 added a tombstone table (`NotFoundPresetSeedRecords`) not anticipated in the plan, so the editor-deletion-persists semantic could be honoured across hard-deletes. New migration `20260518072106_AddPresetSeedRecords` ships this table. The tombstone is exclusively scoped to the `AutoPreset` lifecycle; `ConfigSeeded` reconcile (Task 9) operates only on the live `NotFoundIgnoreRules` table.

Still to verify (covered comprehensively by unit tests, but not exercised live):
- `/wp-admin` hits don't produce `NotFoundHits` rows (requires content-loaded Umbraco environment for the finder to run).
- Adding a `NotFoundTracker.AdditionalAutoPresetRules` entry in `appsettings.json` inserts a `Source=2` (ConfigSeeded) row on next boot.
- Removing that config entry deletes the row on next boot.
- Invalid `MatchType` in config throws at startup.

The unit suite covers all five config-seeded behaviours via real EF Core against in-memory SQLite, so the live test is redundant — but worth one quick round-trip on a content-loaded dev DB before merging the final PR.

## Plan 2 self-review

**Spec coverage:**
- §4 Editor-defined rules: Tasks 1–4 — ✅ exact + prefix match types, hostname scoping, lowercased paths.
- §4 IgnoreRuleMatcher internals (hostname buckets + hash + trie): Tasks 1, 2, 3, 4 — ✅. Volatile snapshot swap covered in Task 4.
- §4 Auto-preset content: Task 6 — ✅ matches spec's grouped list, deliberately excludes `/sitemap.xml`/`/robots.txt`/`/favicon.ico`/`/.well-known/`.
- §4 Config-seeded rules with reconcile-on-boot: Tasks 7, 9 — ✅. Tests cover the four scenarios: insert, delete, separate from UserDefined, separate from AutoPreset. Plus invalid-MatchType fast-fail.
- §8.1 Unit tests: Tasks 1, 3, 4, 8, 9 — ✅. Tasks 5 specifically covers the property-based test against brute-force.
- §8.1 Perf assertion (10k rules, 100k lookups < 500ms on CI): **not implemented in Plan 2**. The property test exercises ~50 rules × 500 paths, which is enough for correctness. The perf assertion is deferred — at current rule counts the optimization isn't necessary, and adding a perf test that flakes on slow CI is worse than no test. Add in Plan 3 only if rule count grows.

**Placeholder scan:** None. Every code block is complete. The `(Task 9 of Plan 2 — added later)` reference in `AutoPresetSeedingService` docstring resolves to a real task in this same plan.

**Type consistency:** Verified across tasks:
- `INotFoundIgnoreRuleMatcher` (from Plan 1) — `IsIgnored` and `RefreshAsync` signatures unchanged in Task 4.
- `IgnoreRuleSnapshot.IsIgnored(hostname, path)` matches `IgnoreRuleMatcher.IsIgnored(hostname, path)` signature.
- `DefaultIgnoreRule(Path, MatchType)` consumed identically in Tasks 6 + 8.
- `ParsedAutoPresetRule(Path, MatchType, Hostname, Note)` consumed identically in Tasks 7 + 9.
- All paths normalized via `UrlNormalizer.NormalizePath` in the parser (Task 7) and pre-lowercased in entities (matches Plan 1's storage convention).

---

## What comes next

**Plan 3** — Management API + multi-tenant permissions + backoffice dashboard. End state: editors curate hits and rules from the Content section, redirect-to-content-node action wired to `IRedirectUrlService`, hostname-scoped permissions derived from user start nodes. Once Plan 1 and Plan 2 are merged, Plan 3 ships the full editor experience described in the design spec.

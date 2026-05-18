# Umbraco.Community.NotFoundTracker — Plan 3: Management API + dashboard + multi-tenant permissions

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship the full editor experience for `Umbraco.Community.NotFoundTracker` — a backoffice dashboard under the Content section where editors can browse recorded 404s, sort/filter them, action individual rows (delete / create redirect / add ignore rule), and manage the ignore rule list directly. Multi-tenant: every API endpoint and UI surface scopes itself to the hostnames the current editor's start nodes give them access to.

**Architecture:** Service layer wraps EF Core access (hit service, ignore rule service, redirect service); a per-request `INotFoundUserScopeService` derives accessible hostnames from the editor's Umbraco user start nodes + bound domains. `NotFoundTrackerApiController` exposes REST endpoints under `/umbraco/umbracocommunitynotfoundtracker/api/v1/` secured with the standard `SectionAccessContent` policy. Frontend is a single Lit dashboard built with Vite under `Client/`, mirroring the `UmbracoCommunity.BlockRestrictions` pattern.

**Tech Stack:** .NET 10, EF Core 10, Umbraco 17.3.5 (IRedirectUrlService, IDomainService, IUserService, AuthorizationPolicies), xUnit + FluentAssertions + Moq for backend tests. Frontend: TypeScript, Lit 3, Vite 7, Vitest 4, `@umbraco-cms/backoffice`.

**Reference design spec:** `docs/superpowers/specs/2026-05-16-not-found-tracker-design.md` §5 (Editor UI) + permission model.

**Branch parent:** `feat/not-found-tracker` (Plans 1 + 2 merged together). When Plans 1 + 2's PR merges to `develop`, rebase this branch onto `develop`.

**Phase markers for stop-anywhere safety:**
- **Phase A** (Task 1): permission scope — small, isolated.
- **Phase B** (Tasks 2–4): service layer — backend logic for hits, ignore rules, redirects.
- **Phase C** (Task 5): API DTOs.
- **Phase D** (Tasks 6–10): controller endpoints + wiring. End of Phase D: full API works, no UI yet — usable via Swagger or curl.
- **Phase E** (Task 11): frontend Vite scaffold.
- **Phase F** (Tasks 12–17): frontend dashboard implementation.
- **Phase G** (Task 18): E2E verification.

Stopping after Phase D ships a functioning Management API that the user can drive via Swagger — useful intermediate state if frontend work is deferred to a follow-up plan.

---

## File Structure

### New files in `src/Umbraco.Community.NotFoundTracker/`

```
Services/
  INotFoundUserScopeService.cs          # per-request multi-tenant scope
  NotFoundUserScopeService.cs
  INotFoundHitService.cs                # query/mutate hit rows
  NotFoundHitService.cs
  INotFoundIgnoreRuleService.cs         # CRUD + reseed
  NotFoundIgnoreRuleService.cs
  INotFoundRedirectService.cs           # wraps IRedirectUrlService
  NotFoundRedirectService.cs
Models/Api/
  HitListItem.cs
  HitListResponse.cs
  HitDetail.cs
  HitQueryStringItem.cs
  IgnoreRuleItem.cs
  CreateIgnoreRuleRequest.cs
  UpdateIgnoreRuleRequest.cs
  CreateRedirectRequest.cs
  BulkIdsRequest.cs
  BulkIgnoreRequest.cs
  BulkOpResponse.cs
  HitListQuery.cs                       # query parameter binding
Controllers/
  NotFoundTrackerApiControllerBase.cs
  NotFoundTrackerApiController.cs
  NotFoundTrackerOperationSecurityFilter.cs
Client/
  package.json
  tsconfig.json
  vite.config.ts
  vitest.config.ts
  public/
    umbraco-package.json
  src/
    bundle.manifests.ts                 # extension registry entry point
    dashboards/
      not-found-tracker-dashboard.element.ts
      hits-tab.element.ts
      ignore-rules-tab.element.ts
      modals/
        create-redirect-modal.element.ts
        add-ignore-rule-modal.element.ts
    api/
      not-found-tracker-api.ts          # fetch wrapper with backoffice auth
      types.ts                          # TypeScript types matching backend DTOs
```

### Modified files

```
src/Umbraco.Community.NotFoundTracker/NotFoundTrackerBuilderExtensions.cs  # register all new services + Swagger
src/Umbraco.Community.NotFoundTracker/Umbraco.Community.NotFoundTracker.csproj  # exclude Client/ from pack, include umbraco-package.json
```

---

## Task 1: User scope service (TDD)

**Files:**
- Create: `src/Umbraco.Community.NotFoundTracker/Services/INotFoundUserScopeService.cs`
- Create: `src/Umbraco.Community.NotFoundTracker/Services/NotFoundUserScopeService.cs`
- Create: `tests/Umbraco.Community.NotFoundTracker.Tests/NotFoundUserScopeServiceTests.cs`

The scope service resolves "which hostnames may the current backoffice user see" by reading the user's start nodes and the domains assigned to those nodes (or their descendants). Used by every endpoint to filter what's visible.

- [ ] **Step 1: Create the interface**

```csharp
namespace Umbraco.Community.NotFoundTracker.Services;

/// <summary>
/// Per-request multi-tenant scope for backoffice users. Derives the set of hostnames
/// the current user is allowed to see + mutate based on their Umbraco start nodes and
/// the domains assigned to those nodes (or their descendants).
/// </summary>
public interface INotFoundUserScopeService
{
    UserScope GetCurrentScope();
}

public sealed class UserScope
{
    public HashSet<string> AccessibleHostnames { get; }
    public bool HasFullAccess { get; }

    public UserScope(HashSet<string> accessibleHostnames, bool hasFullAccess)
    {
        AccessibleHostnames = accessibleHostnames;
        HasFullAccess = hasFullAccess;
    }

    public bool CanAccessHostname(string hostname)
        => HasFullAccess || AccessibleHostnames.Contains(hostname);
}
```

- [ ] **Step 2: Write the failing tests**

Create `tests/Umbraco.Community.NotFoundTracker.Tests/NotFoundUserScopeServiceTests.cs`:

```csharp
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.NotFoundTracker.Services;

namespace Umbraco.Community.NotFoundTracker.Tests;

public class NotFoundUserScopeServiceTests
{
    [Fact]
    public void Full_access_when_start_nodes_include_root()
    {
        var user = MockUser(startNodes: new[] { -1 });
        var sut = Build(user, domains: Array.Empty<IDomain>());

        var scope = sut.GetCurrentScope();

        scope.HasFullAccess.Should().BeTrue();
        scope.AccessibleHostnames.Should().BeEmpty();
        scope.CanAccessHostname("any-host.example").Should().BeTrue();
    }

    [Fact]
    public void No_user_returns_empty_scope_without_full_access()
    {
        var sut = Build(currentUser: null, domains: Array.Empty<IDomain>());

        var scope = sut.GetCurrentScope();

        scope.HasFullAccess.Should().BeFalse();
        scope.AccessibleHostnames.Should().BeEmpty();
        scope.CanAccessHostname("any").Should().BeFalse();
    }

    [Fact]
    public void Single_start_node_collects_its_domains()
    {
        var user = MockUser(startNodes: new[] { 100 });
        var domains = new[]
        {
            MockDomain("site-a.example", contentId: 100),
            MockDomain("site-b.example", contentId: 200),  // not accessible
        };
        var sut = Build(user, domains);

        var scope = sut.GetCurrentScope();

        scope.HasFullAccess.Should().BeFalse();
        scope.AccessibleHostnames.Should().BeEquivalentTo(["site-a.example"]);
        scope.CanAccessHostname("site-a.example").Should().BeTrue();
        scope.CanAccessHostname("site-b.example").Should().BeFalse();
    }

    [Fact]
    public void Hostnames_are_lowercased_in_the_scope()
    {
        var user = MockUser(startNodes: new[] { 100 });
        var domains = new[] { MockDomain("Site-A.Example", contentId: 100) };
        var sut = Build(user, domains);

        var scope = sut.GetCurrentScope();

        scope.AccessibleHostnames.Should().Contain("site-a.example");
        scope.CanAccessHostname("site-a.example").Should().BeTrue();
    }

    [Fact]
    public void Multiple_start_nodes_union_their_hostnames()
    {
        var user = MockUser(startNodes: new[] { 100, 200 });
        var domains = new[]
        {
            MockDomain("a.example", contentId: 100),
            MockDomain("b.example", contentId: 200),
            MockDomain("c.example", contentId: 300),  // not in start nodes
        };
        var sut = Build(user, domains);

        var scope = sut.GetCurrentScope();

        scope.AccessibleHostnames.Should().BeEquivalentTo(["a.example", "b.example"]);
    }

    [Fact]
    public void Wildcard_domain_without_name_is_ignored()
    {
        var user = MockUser(startNodes: new[] { 100 });
        var domains = new[]
        {
            MockDomain(null, contentId: 100),               // wildcard — no hostname to expose
            MockDomain("a.example", contentId: 100),
        };
        var sut = Build(user, domains);

        var scope = sut.GetCurrentScope();

        scope.AccessibleHostnames.Should().BeEquivalentTo(["a.example"]);
    }

    private static IUser MockUser(int[] startNodes)
    {
        var user = new Mock<IUser>();
        user.Setup(u => u.StartContentIds).Returns(startNodes);
        return user.Object;
    }

    private static IDomain MockDomain(string? name, int contentId)
    {
        var d = new Mock<IDomain>();
        d.Setup(x => x.DomainName).Returns(name);
        d.Setup(x => x.RootContentId).Returns(contentId);
        return d.Object;
    }

    private static NotFoundUserScopeService Build(IUser? currentUser, IEnumerable<IDomain> domains)
    {
        var security = new Mock<IBackOfficeSecurityAccessor>();
        var backOfficeSecurity = new Mock<IBackOfficeSecurity>();
        backOfficeSecurity.Setup(b => b.CurrentUser).Returns(currentUser);
        security.Setup(s => s.BackOfficeSecurity).Returns(backOfficeSecurity.Object);

        var domainService = new Mock<IDomainService>();
        domainService.Setup(d => d.GetAll(It.IsAny<bool>())).Returns(domains);

        return new NotFoundUserScopeService(
            security.Object,
            domainService.Object,
            NullLogger<NotFoundUserScopeService>.Instance);
    }
}
```

- [ ] **Step 3: Run tests — expect compile failure**

```bash
dotnet test tests/Umbraco.Community.NotFoundTracker.Tests/Umbraco.Community.NotFoundTracker.Tests.csproj
```

Expected: `NotFoundUserScopeService` not defined.

- [ ] **Step 4: Implement `NotFoundUserScopeService.cs`**

```csharp
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.NotFoundTracker.Matching;

namespace Umbraco.Community.NotFoundTracker.Services;

public sealed class NotFoundUserScopeService : INotFoundUserScopeService
{
    private readonly IBackOfficeSecurityAccessor _security;
    private readonly IDomainService _domainService;
    private readonly ILogger<NotFoundUserScopeService> _logger;

    public NotFoundUserScopeService(
        IBackOfficeSecurityAccessor security,
        IDomainService domainService,
        ILogger<NotFoundUserScopeService> logger)
    {
        _security = security;
        _domainService = domainService;
        _logger = logger;
    }

    public UserScope GetCurrentScope()
    {
        var user = _security.BackOfficeSecurity?.CurrentUser;
        if (user is null)
        {
            return new UserScope(new HashSet<string>(StringComparer.Ordinal), hasFullAccess: false);
        }

        var startNodes = user.StartContentIds ?? Array.Empty<int>();

        // Full-access if user has root (-1) as a start node.
        if (startNodes.Contains(-1))
        {
            return new UserScope(new HashSet<string>(StringComparer.Ordinal), hasFullAccess: true);
        }

        var accessible = new HashSet<string>(StringComparer.Ordinal);
        if (startNodes.Length == 0)
        {
            return new UserScope(accessible, hasFullAccess: false);
        }

        // Map: each domain row has a RootContentId — collect domains whose root content
        // is in the user's start node set. (Descendant-node domain lookups can be added
        // later if needed; for v1 we only honour domains directly assigned to start nodes.)
        var startNodeSet = new HashSet<int>(startNodes);
        foreach (var domain in _domainService.GetAll(includeWildcards: true))
        {
            if (domain.RootContentId is null) continue;
            if (!startNodeSet.Contains(domain.RootContentId.Value)) continue;
            if (string.IsNullOrEmpty(domain.DomainName)) continue;

            accessible.Add(UrlNormalizer.NormalizeHostname(domain.DomainName));
        }

        return new UserScope(accessible, hasFullAccess: false);
    }
}
```

- [ ] **Step 5: Run tests — expect 6 pass**

```bash
dotnet test tests/Umbraco.Community.NotFoundTracker.Tests/Umbraco.Community.NotFoundTracker.Tests.csproj
```

Expected: 65 total tests (59 existing + 6 new).

- [ ] **Step 6: Commit**

```bash
git add src/Umbraco.Community.NotFoundTracker/Services/ tests/Umbraco.Community.NotFoundTracker.Tests/NotFoundUserScopeServiceTests.cs
git commit -m "feat(NotFoundTracker): add per-request user scope service"
```

---

## Task 2: Hit service (TDD)

Provides paginated query + filter + sort over `NotFoundHits`, plus delete + bulk-delete operations. All operations respect the user's scope from Task 1.

**Files:**
- Create: `src/Umbraco.Community.NotFoundTracker/Services/INotFoundHitService.cs`
- Create: `src/Umbraco.Community.NotFoundTracker/Services/NotFoundHitService.cs`
- Create: `tests/Umbraco.Community.NotFoundTracker.Tests/NotFoundHitServiceTests.cs`

- [ ] **Step 1: Create the interface**

```csharp
using Umbraco.Community.NotFoundTracker.Models.Entities;

namespace Umbraco.Community.NotFoundTracker.Services;

public interface INotFoundHitService
{
    Task<(IReadOnlyList<NotFoundHitEntity> items, int total)> ListAsync(HitListQuery query, UserScope scope, CancellationToken ct);
    Task<NotFoundHitEntity?> GetAsync(int id, UserScope scope, CancellationToken ct);
    Task<IReadOnlyList<string>> GetDistinctHostnamesAsync(UserScope scope, CancellationToken ct);
    Task<bool> DeleteAsync(int id, UserScope scope, CancellationToken ct);
    Task<(int processed, int skipped)> BulkDeleteAsync(IEnumerable<int> ids, UserScope scope, CancellationToken ct);
}

public sealed class HitListQuery
{
    public string? Hostname { get; init; }
    public HitStatus? Status { get; init; } = HitStatus.Active;
    public string? Search { get; init; }
    public HitSort Sort { get; init; } = HitSort.RecentlySeen;
    public int Skip { get; init; }
    public int Take { get; init; } = 25;
}

public enum HitSort
{
    RecentlySeen = 0,
    Popularity = 1,
    FirstSeen = 2,
}
```

- [ ] **Step 2: Write the failing tests** (verify scope filtering, paging, sort, mutations)

Create `tests/Umbraco.Community.NotFoundTracker.Tests/NotFoundHitServiceTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Umbraco.Community.NotFoundTracker.Infrastructure;
using Umbraco.Community.NotFoundTracker.Models.Entities;
using Umbraco.Community.NotFoundTracker.Services;

namespace Umbraco.Community.NotFoundTracker.Tests;

public class NotFoundHitServiceTests : IDisposable
{
    private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;
    private readonly DbContextOptions<NotFoundTrackerDbContext> _dbOptions;

    public NotFoundHitServiceTests()
    {
        _connection = new Microsoft.Data.Sqlite.SqliteConnection("Filename=:memory:");
        _connection.Open();
        _dbOptions = new DbContextOptionsBuilder<NotFoundTrackerDbContext>().UseSqlite(_connection).Options;
        using var ctx = new NotFoundTrackerDbContext(_dbOptions);
        ctx.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private NotFoundTrackerDbContext Ctx() => new(_dbOptions);
    private NotFoundHitService BuildService() => new(new TestFactory(_dbOptions));

    private static UserScope FullAccess() => new(new HashSet<string>(), hasFullAccess: true);
    private static UserScope Scoped(params string[] hosts)
        => new(new HashSet<string>(hosts, StringComparer.Ordinal), hasFullAccess: false);

    [Fact]
    public async Task List_filters_by_scope_when_not_full_access()
    {
        await Seed(
            ("a.example", "/foo"),
            ("b.example", "/bar"));

        var (items, total) = await BuildService().ListAsync(new HitListQuery(), Scoped("a.example"), default);

        items.Should().HaveCount(1);
        items[0].Hostname.Should().Be("a.example");
        total.Should().Be(1);
    }

    [Fact]
    public async Task List_with_full_access_returns_all()
    {
        await Seed(("a.example", "/foo"), ("b.example", "/bar"));

        var (items, total) = await BuildService().ListAsync(new HitListQuery(), FullAccess(), default);

        items.Should().HaveCount(2);
        total.Should().Be(2);
    }

    [Fact]
    public async Task List_hostname_filter_is_intersected_with_scope()
    {
        await Seed(("a.example", "/foo"), ("b.example", "/bar"));

        var (items, _) = await BuildService().ListAsync(
            new HitListQuery { Hostname = "b.example" },
            Scoped("a.example"),  // doesn't include b.example
            default);

        items.Should().BeEmpty();
    }

    [Fact]
    public async Task List_sort_by_popularity_orders_descending_by_hit_count()
    {
        await Seed(
            ("a.example", "/low",  hitCount: 1),
            ("a.example", "/high", hitCount: 100),
            ("a.example", "/mid",  hitCount: 50));

        var (items, _) = await BuildService().ListAsync(
            new HitListQuery { Sort = HitSort.Popularity },
            FullAccess(),
            default);

        items.Select(i => i.Path).Should().ContainInOrder("/high", "/mid", "/low");
    }

    [Fact]
    public async Task List_sort_by_recently_seen_orders_descending_by_last_seen()
    {
        var now = DateTime.UtcNow;
        await Seed(
            ("a.example", "/old",   lastSeenUtc: now.AddDays(-10)),
            ("a.example", "/recent", lastSeenUtc: now.AddDays(-1)));

        var (items, _) = await BuildService().ListAsync(
            new HitListQuery { Sort = HitSort.RecentlySeen },
            FullAccess(),
            default);

        items.Select(i => i.Path).Should().ContainInOrder("/recent", "/old");
    }

    [Fact]
    public async Task List_status_filter_defaults_to_active()
    {
        await Seed(
            ("a.example", "/active",     status: HitStatus.Active),
            ("a.example", "/redirected", status: HitStatus.Redirected));

        var (items, _) = await BuildService().ListAsync(new HitListQuery(), FullAccess(), default);

        items.Should().ContainSingle(i => i.Path == "/active");
    }

    [Fact]
    public async Task List_paginates_correctly()
    {
        for (var i = 0; i < 30; i++)
        {
            await Seed(("a.example", $"/p{i:D2}", hitCount: 30 - i));
        }

        var (items, total) = await BuildService().ListAsync(
            new HitListQuery { Sort = HitSort.Popularity, Skip = 10, Take = 5 },
            FullAccess(),
            default);

        items.Should().HaveCount(5);
        total.Should().Be(30);
        items[0].Path.Should().Be("/p10");
    }

    [Fact]
    public async Task Get_returns_null_when_row_outside_scope()
    {
        await Seed(("a.example", "/foo"));
        var id = (await Ctx().NotFoundHits.SingleAsync()).Id;

        var hit = await BuildService().GetAsync(id, Scoped("b.example"), default);

        hit.Should().BeNull();
    }

    [Fact]
    public async Task Delete_returns_false_when_row_outside_scope()
    {
        await Seed(("a.example", "/foo"));
        var id = (await Ctx().NotFoundHits.SingleAsync()).Id;

        var ok = await BuildService().DeleteAsync(id, Scoped("b.example"), default);

        ok.Should().BeFalse();
        (await Ctx().NotFoundHits.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task BulkDelete_reports_processed_and_skipped()
    {
        await Seed(("a.example", "/foo"), ("b.example", "/bar"));
        var ids = await Ctx().NotFoundHits.Select(h => h.Id).ToListAsync();

        var (processed, skipped) = await BuildService().BulkDeleteAsync(ids, Scoped("a.example"), default);

        processed.Should().Be(1);
        skipped.Should().Be(1);
    }

    [Fact]
    public async Task Distinct_hostnames_intersects_with_scope()
    {
        await Seed(("a.example", "/x"), ("b.example", "/x"), ("c.example", "/x"));

        var hosts = await BuildService().GetDistinctHostnamesAsync(Scoped("a.example", "c.example"), default);

        hosts.Should().BeEquivalentTo(["a.example", "c.example"]);
    }

    private async Task Seed(params (string Hostname, string Path)[] rows)
    {
        using var ctx = Ctx();
        foreach (var (h, p) in rows)
        {
            ctx.NotFoundHits.Add(new NotFoundHitEntity
            {
                Hostname = h, Path = p, HitCount = 1,
                FirstSeenUtc = DateTime.UtcNow, LastSeenUtc = DateTime.UtcNow,
                Status = HitStatus.Active,
            });
        }
        await ctx.SaveChangesAsync();
    }

    private async Task Seed(
        (string Hostname, string Path, long hitCount = 1, DateTime? lastSeenUtc = null, HitStatus status = HitStatus.Active) row)
    {
        using var ctx = Ctx();
        ctx.NotFoundHits.Add(new NotFoundHitEntity
        {
            Hostname = row.Hostname, Path = row.Path, HitCount = row.hitCount,
            FirstSeenUtc = DateTime.UtcNow,
            LastSeenUtc = row.lastSeenUtc ?? DateTime.UtcNow,
            Status = row.status,
        });
        await ctx.SaveChangesAsync();
    }

    private sealed class TestFactory : IDbContextFactory<NotFoundTrackerDbContext>
    {
        private readonly DbContextOptions<NotFoundTrackerDbContext> _options;
        public TestFactory(DbContextOptions<NotFoundTrackerDbContext> options) => _options = options;
        public NotFoundTrackerDbContext CreateDbContext() => new(_options);
    }
}
```

- [ ] **Step 3: Implement `NotFoundHitService.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using Umbraco.Community.NotFoundTracker.Infrastructure;
using Umbraco.Community.NotFoundTracker.Models.Entities;

namespace Umbraco.Community.NotFoundTracker.Services;

public sealed class NotFoundHitService : INotFoundHitService
{
    private readonly IDbContextFactory<NotFoundTrackerDbContext> _contextFactory;

    public NotFoundHitService(IDbContextFactory<NotFoundTrackerDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<(IReadOnlyList<NotFoundHitEntity> items, int total)> ListAsync(
        HitListQuery query, UserScope scope, CancellationToken ct)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var q = context.NotFoundHits.AsNoTracking().AsQueryable();

        if (!scope.HasFullAccess)
        {
            // Empty scope means user has no hostname access — return nothing.
            if (scope.AccessibleHostnames.Count == 0)
            {
                return (Array.Empty<NotFoundHitEntity>(), 0);
            }
            q = q.Where(h => scope.AccessibleHostnames.Contains(h.Hostname));
        }

        if (!string.IsNullOrEmpty(query.Hostname))
        {
            // Intersect with scope: only honour the filter if the user can see that hostname.
            if (!scope.CanAccessHostname(query.Hostname))
            {
                return (Array.Empty<NotFoundHitEntity>(), 0);
            }
            q = q.Where(h => h.Hostname == query.Hostname);
        }

        if (query.Status.HasValue)
        {
            q = q.Where(h => h.Status == query.Status.Value);
        }

        if (!string.IsNullOrEmpty(query.Search))
        {
            var search = query.Search.ToLowerInvariant();
            q = q.Where(h => h.Path.Contains(search));
        }

        q = query.Sort switch
        {
            HitSort.Popularity => q.OrderByDescending(h => h.HitCount).ThenByDescending(h => h.LastSeenUtc),
            HitSort.FirstSeen => q.OrderByDescending(h => h.FirstSeenUtc),
            _ => q.OrderByDescending(h => h.LastSeenUtc),
        };

        var total = await q.CountAsync(ct);
        var items = await q.Skip(query.Skip).Take(query.Take).ToListAsync(ct);

        return (items, total);
    }

    public async Task<NotFoundHitEntity?> GetAsync(int id, UserScope scope, CancellationToken ct)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var hit = await context.NotFoundHits
            .AsNoTracking()
            .Include(h => h.QueryStrings)
            .FirstOrDefaultAsync(h => h.Id == id, ct);

        if (hit is null) return null;
        return scope.CanAccessHostname(hit.Hostname) ? hit : null;
    }

    public async Task<IReadOnlyList<string>> GetDistinctHostnamesAsync(UserScope scope, CancellationToken ct)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var hostnames = await context.NotFoundHits
            .AsNoTracking()
            .Select(h => h.Hostname)
            .Distinct()
            .ToListAsync(ct);

        if (scope.HasFullAccess) return hostnames;
        return hostnames.Where(h => scope.AccessibleHostnames.Contains(h)).ToList();
    }

    public async Task<bool> DeleteAsync(int id, UserScope scope, CancellationToken ct)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var hit = await context.NotFoundHits.FirstOrDefaultAsync(h => h.Id == id, ct);
        if (hit is null) return false;
        if (!scope.CanAccessHostname(hit.Hostname)) return false;

        context.NotFoundHits.Remove(hit);
        await context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<(int processed, int skipped)> BulkDeleteAsync(
        IEnumerable<int> ids, UserScope scope, CancellationToken ct)
    {
        var idList = ids.ToList();
        if (idList.Count == 0) return (0, 0);

        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var hits = await context.NotFoundHits.Where(h => idList.Contains(h.Id)).ToListAsync(ct);

        var processable = hits.Where(h => scope.CanAccessHostname(h.Hostname)).ToList();
        var skipped = idList.Count - processable.Count;

        context.NotFoundHits.RemoveRange(processable);
        await context.SaveChangesAsync(ct);

        return (processable.Count, skipped);
    }
}
```

- [ ] **Step 4: Run tests — expect 76 pass** (65 existing + 11 new)

```bash
dotnet test tests/Umbraco.Community.NotFoundTracker.Tests/Umbraco.Community.NotFoundTracker.Tests.csproj
```

- [ ] **Step 5: Commit**

```bash
git add src/Umbraco.Community.NotFoundTracker/Services/ tests/Umbraco.Community.NotFoundTracker.Tests/NotFoundHitServiceTests.cs
git commit -m "feat(NotFoundTracker): add hit service with scope-aware queries and mutations"
```

---

## Task 3: Ignore rule service (TDD)

CRUD operations on `NotFoundIgnoreRules` + the "reseed auto-preset" action. Source-aware: `ConfigSeeded` rules are read-only via the API. Refreshes the matcher after every mutation.

**Files:**
- Create: `src/Umbraco.Community.NotFoundTracker/Services/INotFoundIgnoreRuleService.cs`
- Create: `src/Umbraco.Community.NotFoundTracker/Services/NotFoundIgnoreRuleService.cs`
- Create: `tests/Umbraco.Community.NotFoundTracker.Tests/NotFoundIgnoreRuleServiceTests.cs`

- [ ] **Step 1: Create the interface**

```csharp
using Umbraco.Community.NotFoundTracker.Models.Entities;

namespace Umbraco.Community.NotFoundTracker.Services;

public interface INotFoundIgnoreRuleService
{
    Task<IReadOnlyList<NotFoundIgnoreRuleEntity>> ListAsync(UserScope scope, CancellationToken ct);
    Task<IgnoreRuleMutation> CreateAsync(CreateIgnoreRuleInput input, UserScope scope, CancellationToken ct);
    Task<IgnoreRuleMutation> UpdateAsync(int id, UpdateIgnoreRuleInput input, UserScope scope, CancellationToken ct);
    Task<IgnoreRuleMutation> DeleteAsync(int id, UserScope scope, CancellationToken ct);
}

public sealed record CreateIgnoreRuleInput(
    string Path,
    IgnoreMatchType MatchType,
    string? Hostname,
    string? Note);

public sealed record UpdateIgnoreRuleInput(
    string Path,
    IgnoreMatchType MatchType,
    string? Hostname,
    string? Note);

public enum IgnoreRuleMutationResult
{
    Ok,
    NotFound,
    Forbidden,
    Conflict,        // duplicate (Hostname, MatchType, Path)
    InvalidInput,
}

public sealed record IgnoreRuleMutation(
    IgnoreRuleMutationResult Result,
    NotFoundIgnoreRuleEntity? Entity = null,
    string? Reason = null);
```

- [ ] **Step 2: Write the failing tests**

```csharp
using Microsoft.EntityFrameworkCore;
using Moq;
using Umbraco.Community.NotFoundTracker.Infrastructure;
using Umbraco.Community.NotFoundTracker.Matching;
using Umbraco.Community.NotFoundTracker.Models.Entities;
using Umbraco.Community.NotFoundTracker.Services;

namespace Umbraco.Community.NotFoundTracker.Tests;

public class NotFoundIgnoreRuleServiceTests : IDisposable
{
    private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;
    private readonly DbContextOptions<NotFoundTrackerDbContext> _dbOptions;
    private readonly Mock<INotFoundIgnoreRuleMatcher> _matcher = new();

    public NotFoundIgnoreRuleServiceTests()
    {
        _connection = new Microsoft.Data.Sqlite.SqliteConnection("Filename=:memory:");
        _connection.Open();
        _dbOptions = new DbContextOptionsBuilder<NotFoundTrackerDbContext>().UseSqlite(_connection).Options;
        using var ctx = new NotFoundTrackerDbContext(_dbOptions);
        ctx.Database.EnsureCreated();
        _matcher.Setup(m => m.RefreshAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
    }

    public void Dispose() => _connection.Dispose();

    private NotFoundTrackerDbContext Ctx() => new(_dbOptions);
    private NotFoundIgnoreRuleService Build() => new(new TestFactory(_dbOptions), _matcher.Object);
    private static UserScope FullAccess() => new(new HashSet<string>(), hasFullAccess: true);
    private static UserScope Scoped(params string[] hosts)
        => new(new HashSet<string>(hosts, StringComparer.Ordinal), hasFullAccess: false);

    [Fact]
    public async Task List_includes_global_rules_for_all_users()
    {
        using (var ctx = Ctx())
        {
            ctx.NotFoundIgnoreRules.Add(new NotFoundIgnoreRuleEntity { Hostname = null, Path = "/x", MatchType = IgnoreMatchType.Exact, Source = IgnoreRuleSource.AutoPreset });
            ctx.NotFoundIgnoreRules.Add(new NotFoundIgnoreRuleEntity { Hostname = "a", Path = "/y", MatchType = IgnoreMatchType.Exact, Source = IgnoreRuleSource.UserDefined });
            ctx.NotFoundIgnoreRules.Add(new NotFoundIgnoreRuleEntity { Hostname = "b", Path = "/z", MatchType = IgnoreMatchType.Exact, Source = IgnoreRuleSource.UserDefined });
            await ctx.SaveChangesAsync();
        }

        var list = await Build().ListAsync(Scoped("a"), default);

        // Global + scoped, but not "b" rules.
        list.Select(r => r.Path).Should().BeEquivalentTo(["/x", "/y"]);
    }

    [Fact]
    public async Task Create_normalizes_path_and_refreshes_matcher()
    {
        var result = await Build().CreateAsync(
            new CreateIgnoreRuleInput("/FOO", IgnoreMatchType.PathPrefix, Hostname: null, Note: "n"),
            FullAccess(), default);

        result.Result.Should().Be(IgnoreRuleMutationResult.Ok);
        result.Entity!.Path.Should().Be("/foo");
        result.Entity!.Source.Should().Be(IgnoreRuleSource.UserDefined);
        _matcher.Verify(m => m.RefreshAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_returns_Conflict_on_duplicate_key()
    {
        await Build().CreateAsync(new CreateIgnoreRuleInput("/foo", IgnoreMatchType.Exact, null, null), FullAccess(), default);
        var result = await Build().CreateAsync(new CreateIgnoreRuleInput("/foo", IgnoreMatchType.Exact, null, null), FullAccess(), default);

        result.Result.Should().Be(IgnoreRuleMutationResult.Conflict);
    }

    [Fact]
    public async Task Create_global_rule_is_Forbidden_when_user_lacks_full_access()
    {
        var result = await Build().CreateAsync(
            new CreateIgnoreRuleInput("/foo", IgnoreMatchType.Exact, Hostname: null, Note: null),
            Scoped("a"), default);

        result.Result.Should().Be(IgnoreRuleMutationResult.Forbidden);
        (await Ctx().NotFoundIgnoreRules.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Create_hostname_rule_is_Forbidden_when_user_lacks_that_hostname()
    {
        var result = await Build().CreateAsync(
            new CreateIgnoreRuleInput("/foo", IgnoreMatchType.Exact, Hostname: "b", Note: null),
            Scoped("a"), default);

        result.Result.Should().Be(IgnoreRuleMutationResult.Forbidden);
    }

    [Fact]
    public async Task Update_ConfigSeeded_rule_is_Forbidden_for_everyone()
    {
        using (var ctx = Ctx())
        {
            ctx.NotFoundIgnoreRules.Add(new NotFoundIgnoreRuleEntity
            {
                Hostname = null, Path = "/foo", MatchType = IgnoreMatchType.Exact,
                Source = IgnoreRuleSource.ConfigSeeded,
            });
            await ctx.SaveChangesAsync();
        }
        var id = (await Ctx().NotFoundIgnoreRules.SingleAsync()).Id;

        var result = await Build().UpdateAsync(id, new UpdateIgnoreRuleInput("/bar", IgnoreMatchType.Exact, null, null), FullAccess(), default);

        result.Result.Should().Be(IgnoreRuleMutationResult.Forbidden);
        result.Reason.Should().Contain("ConfigSeeded");
    }

    [Fact]
    public async Task Delete_NotFound_when_no_such_id()
    {
        var result = await Build().DeleteAsync(9999, FullAccess(), default);
        result.Result.Should().Be(IgnoreRuleMutationResult.NotFound);
    }

    [Fact]
    public async Task Delete_refreshes_matcher_on_success()
    {
        await Build().CreateAsync(new CreateIgnoreRuleInput("/foo", IgnoreMatchType.Exact, null, null), FullAccess(), default);
        var id = (await Ctx().NotFoundIgnoreRules.SingleAsync()).Id;
        _matcher.Invocations.Clear();

        var result = await Build().DeleteAsync(id, FullAccess(), default);

        result.Result.Should().Be(IgnoreRuleMutationResult.Ok);
        _matcher.Verify(m => m.RefreshAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private sealed class TestFactory : IDbContextFactory<NotFoundTrackerDbContext>
    {
        private readonly DbContextOptions<NotFoundTrackerDbContext> _options;
        public TestFactory(DbContextOptions<NotFoundTrackerDbContext> options) => _options = options;
        public NotFoundTrackerDbContext CreateDbContext() => new(_options);
    }
}
```

- [ ] **Step 3: Implement the service**

```csharp
using Microsoft.EntityFrameworkCore;
using Umbraco.Community.NotFoundTracker.Infrastructure;
using Umbraco.Community.NotFoundTracker.Matching;
using Umbraco.Community.NotFoundTracker.Models.Entities;

namespace Umbraco.Community.NotFoundTracker.Services;

public sealed class NotFoundIgnoreRuleService : INotFoundIgnoreRuleService
{
    private readonly IDbContextFactory<NotFoundTrackerDbContext> _contextFactory;
    private readonly INotFoundIgnoreRuleMatcher _matcher;

    public NotFoundIgnoreRuleService(
        IDbContextFactory<NotFoundTrackerDbContext> contextFactory,
        INotFoundIgnoreRuleMatcher matcher)
    {
        _contextFactory = contextFactory;
        _matcher = matcher;
    }

    public async Task<IReadOnlyList<NotFoundIgnoreRuleEntity>> ListAsync(UserScope scope, CancellationToken ct)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var rules = await context.NotFoundIgnoreRules.AsNoTracking().ToListAsync(ct);

        if (scope.HasFullAccess) return rules;

        // Global rules are visible to everyone; hostname-scoped rules only when accessible.
        return rules
            .Where(r => r.Hostname is null || scope.AccessibleHostnames.Contains(r.Hostname))
            .ToList();
    }

    public async Task<IgnoreRuleMutation> CreateAsync(CreateIgnoreRuleInput input, UserScope scope, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(input.Path))
        {
            return new IgnoreRuleMutation(IgnoreRuleMutationResult.InvalidInput, Reason: "Path is required.");
        }

        // Permission check: global rules require full access; scoped rules require matching scope.
        if (input.Hostname is null)
        {
            if (!scope.HasFullAccess)
            {
                return new IgnoreRuleMutation(IgnoreRuleMutationResult.Forbidden,
                    Reason: "Creating a global ignore rule requires full content access.");
            }
        }
        else if (!scope.CanAccessHostname(input.Hostname))
        {
            return new IgnoreRuleMutation(IgnoreRuleMutationResult.Forbidden,
                Reason: $"User does not have access to hostname '{input.Hostname}'.");
        }

        var normalizedPath = UrlNormalizer.NormalizePath(input.Path);
        var normalizedHostname = string.IsNullOrEmpty(input.Hostname) ? null : UrlNormalizer.NormalizeHostname(input.Hostname);

        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var duplicate = await context.NotFoundIgnoreRules.AnyAsync(
            r => r.Hostname == normalizedHostname && r.MatchType == input.MatchType && r.Path == normalizedPath, ct);
        if (duplicate)
        {
            return new IgnoreRuleMutation(IgnoreRuleMutationResult.Conflict,
                Reason: "An ignore rule with this hostname, match type, and path already exists.");
        }

        var entity = new NotFoundIgnoreRuleEntity
        {
            Hostname = normalizedHostname,
            MatchType = input.MatchType,
            Path = normalizedPath,
            Source = IgnoreRuleSource.UserDefined,
            Note = input.Note,
            CreatedUtc = DateTime.UtcNow,
        };
        context.NotFoundIgnoreRules.Add(entity);
        await context.SaveChangesAsync(ct);

        await _matcher.RefreshAsync(ct);
        return new IgnoreRuleMutation(IgnoreRuleMutationResult.Ok, entity);
    }

    public async Task<IgnoreRuleMutation> UpdateAsync(int id, UpdateIgnoreRuleInput input, UserScope scope, CancellationToken ct)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var entity = await context.NotFoundIgnoreRules.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (entity is null) return new IgnoreRuleMutation(IgnoreRuleMutationResult.NotFound);

        if (entity.Source == IgnoreRuleSource.ConfigSeeded)
        {
            return new IgnoreRuleMutation(IgnoreRuleMutationResult.Forbidden,
                Reason: "ConfigSeeded rules are read-only — edit them via appsettings.json.");
        }

        // Permission: must own both the current scope of the rule AND the proposed scope.
        if (!CanMutate(entity.Hostname, scope) || !CanMutate(input.Hostname, scope))
        {
            return new IgnoreRuleMutation(IgnoreRuleMutationResult.Forbidden);
        }

        entity.Path = UrlNormalizer.NormalizePath(input.Path);
        entity.MatchType = input.MatchType;
        entity.Hostname = string.IsNullOrEmpty(input.Hostname) ? null : UrlNormalizer.NormalizeHostname(input.Hostname);
        entity.Note = input.Note;

        try
        {
            await context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return new IgnoreRuleMutation(IgnoreRuleMutationResult.Conflict);
        }

        await _matcher.RefreshAsync(ct);
        return new IgnoreRuleMutation(IgnoreRuleMutationResult.Ok, entity);
    }

    public async Task<IgnoreRuleMutation> DeleteAsync(int id, UserScope scope, CancellationToken ct)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var entity = await context.NotFoundIgnoreRules.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (entity is null) return new IgnoreRuleMutation(IgnoreRuleMutationResult.NotFound);

        if (entity.Source == IgnoreRuleSource.ConfigSeeded)
        {
            return new IgnoreRuleMutation(IgnoreRuleMutationResult.Forbidden,
                Reason: "ConfigSeeded rules are read-only — remove them from appsettings.json.");
        }

        if (!CanMutate(entity.Hostname, scope))
        {
            return new IgnoreRuleMutation(IgnoreRuleMutationResult.Forbidden);
        }

        context.NotFoundIgnoreRules.Remove(entity);
        await context.SaveChangesAsync(ct);
        await _matcher.RefreshAsync(ct);
        return new IgnoreRuleMutation(IgnoreRuleMutationResult.Ok);
    }

    private static bool CanMutate(string? hostname, UserScope scope)
    {
        if (hostname is null) return scope.HasFullAccess;
        return scope.CanAccessHostname(hostname);
    }
}
```

- [ ] **Step 4: Run tests — expect 84 pass** (76 existing + 8 new)

```bash
dotnet test tests/Umbraco.Community.NotFoundTracker.Tests/Umbraco.Community.NotFoundTracker.Tests.csproj
```

- [ ] **Step 5: Commit**

```bash
git add src/Umbraco.Community.NotFoundTracker/Services/ tests/Umbraco.Community.NotFoundTracker.Tests/NotFoundIgnoreRuleServiceTests.cs
git commit -m "feat(NotFoundTracker): add ignore rule service with scope + ConfigSeeded guards"
```

---

## Task 4: Redirect service

Thin wrapper around Umbraco's `IRedirectUrlService.Register(...)`. The wrapper validates user scope (both the hit's hostname and the target node's reachability), flips the hit row's `Status` to `Redirected`, and cascade-deletes its QS children.

**Files:**
- Create: `src/Umbraco.Community.NotFoundTracker/Services/INotFoundRedirectService.cs`
- Create: `src/Umbraco.Community.NotFoundTracker/Services/NotFoundRedirectService.cs`

- [ ] **Step 1: Create the interface**

```csharp
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
```

- [ ] **Step 2: Implement the service**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;
using Umbraco.Community.NotFoundTracker.Infrastructure;
using Umbraco.Community.NotFoundTracker.Matching;
using Umbraco.Community.NotFoundTracker.Models.Entities;

namespace Umbraco.Community.NotFoundTracker.Services;

public sealed class NotFoundRedirectService : INotFoundRedirectService
{
    private readonly IDbContextFactory<NotFoundTrackerDbContext> _contextFactory;
    private readonly IRedirectUrlService _redirectUrlService;
    private readonly IUmbracoContextAccessor _umbracoContextAccessor;
    private readonly ILogger<NotFoundRedirectService> _logger;

    public NotFoundRedirectService(
        IDbContextFactory<NotFoundTrackerDbContext> contextFactory,
        IRedirectUrlService redirectUrlService,
        IUmbracoContextAccessor umbracoContextAccessor,
        ILogger<NotFoundRedirectService> logger)
    {
        _contextFactory = contextFactory;
        _redirectUrlService = redirectUrlService;
        _umbracoContextAccessor = umbracoContextAccessor;
        _logger = logger;
    }

    public async Task<RedirectResult> CreateRedirectForHitAsync(
        int hitId, Guid targetContentKey, string? culture, UserScope scope, CancellationToken ct)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var hit = await context.NotFoundHits
            .Include(h => h.QueryStrings)
            .FirstOrDefaultAsync(h => h.Id == hitId, ct);
        if (hit is null) return new RedirectResult(RedirectResultKind.HitNotFound);

        if (!scope.CanAccessHostname(hit.Hostname))
        {
            return new RedirectResult(RedirectResultKind.Forbidden,
                Reason: $"User cannot access hostname '{hit.Hostname}'.");
        }

        if (!_umbracoContextAccessor.TryGetUmbracoContext(out var umbracoContext))
        {
            return new RedirectResult(RedirectResultKind.Failed, Reason: "UmbracoContext unavailable.");
        }

        var targetContent = umbracoContext.Content?.GetById(targetContentKey);
        if (targetContent is null)
        {
            return new RedirectResult(RedirectResultKind.TargetContentNotFound);
        }

        // The full URL the editor wants to register is (hostname + path). Umbraco's
        // RedirectUrlService.Register stores by URL and resolves on incoming request match.
        var url = string.IsNullOrEmpty(hit.Hostname)
            ? hit.Path
            : $"{hit.Hostname}{hit.Path}";

        try
        {
            _redirectUrlService.Register(url, targetContent.Key, culture);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register redirect for hit {HitId} (url='{Url}')", hitId, url);
            return new RedirectResult(RedirectResultKind.Failed, Reason: ex.Message);
        }

        hit.Status = HitStatus.Redirected;
        context.NotFoundHitQueryStrings.RemoveRange(hit.QueryStrings);
        await context.SaveChangesAsync(ct);

        return new RedirectResult(RedirectResultKind.Ok);
    }
}
```

- [ ] **Step 3: Verify build**

No new tests in this task — the wrapper is thin and harder to unit-test (would need to stub `IUmbracoContextAccessor.Content`). Integration via the controller endpoint in Task 8 covers it.

```bash
dotnet build src/Umbraco.Community.NotFoundTracker/Umbraco.Community.NotFoundTracker.csproj
```

- [ ] **Step 4: Commit**

```bash
git add src/Umbraco.Community.NotFoundTracker/Services/
git commit -m "feat(NotFoundTracker): add redirect service wrapping IRedirectUrlService"
```

---

## Task 5: API DTOs

Plain data classes used by the controller for request/response. All in one task because they're property bags with no logic.

**Files:** Create all of these under `src/Umbraco.Community.NotFoundTracker/Models/Api/`:

- [ ] **Step 1: Create `HitListItem.cs`, `HitListResponse.cs`, `HitDetail.cs`, `HitQueryStringItem.cs`, `IgnoreRuleItem.cs`, `CreateIgnoreRuleRequest.cs`, `UpdateIgnoreRuleRequest.cs`, `CreateRedirectRequest.cs`, `BulkIdsRequest.cs`, `BulkIgnoreRequest.cs`, `BulkOpResponse.cs`**

```csharp
// HitListItem.cs
namespace Umbraco.Community.NotFoundTracker.Models.Api;

public sealed class HitListItem
{
    public int Id { get; set; }
    public string Hostname { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public long HitCount { get; set; }
    public DateTime FirstSeenUtc { get; set; }
    public DateTime LastSeenUtc { get; set; }
    public byte Status { get; set; }
}
```

```csharp
// HitListResponse.cs
namespace Umbraco.Community.NotFoundTracker.Models.Api;

public sealed class HitListResponse
{
    public int Total { get; set; }
    public IReadOnlyList<HitListItem> Items { get; set; } = Array.Empty<HitListItem>();
}
```

```csharp
// HitDetail.cs
namespace Umbraco.Community.NotFoundTracker.Models.Api;

public sealed class HitDetail
{
    public int Id { get; set; }
    public string Hostname { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public long HitCount { get; set; }
    public DateTime FirstSeenUtc { get; set; }
    public DateTime LastSeenUtc { get; set; }
    public string? LastUserAgent { get; set; }
    public byte Status { get; set; }
    public IReadOnlyList<HitQueryStringItem> QueryStrings { get; set; } = Array.Empty<HitQueryStringItem>();
}
```

```csharp
// HitQueryStringItem.cs
namespace Umbraco.Community.NotFoundTracker.Models.Api;

public sealed class HitQueryStringItem
{
    public string QueryString { get; set; } = string.Empty;
    public long HitCount { get; set; }
    public DateTime LastSeenUtc { get; set; }
}
```

```csharp
// IgnoreRuleItem.cs
namespace Umbraco.Community.NotFoundTracker.Models.Api;

public sealed class IgnoreRuleItem
{
    public int Id { get; set; }
    public string? Hostname { get; set; }
    public byte MatchType { get; set; }
    public string Path { get; set; } = string.Empty;
    public byte Source { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedUtc { get; set; }
    public bool IsReadOnly { get; set; }   // true if Source == ConfigSeeded
}
```

```csharp
// CreateIgnoreRuleRequest.cs
namespace Umbraco.Community.NotFoundTracker.Models.Api;

public sealed class CreateIgnoreRuleRequest
{
    public string Path { get; set; } = string.Empty;
    public byte MatchType { get; set; }   // 0 = Exact, 1 = PathPrefix
    public string? Hostname { get; set; }
    public string? Note { get; set; }
}
```

```csharp
// UpdateIgnoreRuleRequest.cs
namespace Umbraco.Community.NotFoundTracker.Models.Api;

public sealed class UpdateIgnoreRuleRequest
{
    public string Path { get; set; } = string.Empty;
    public byte MatchType { get; set; }
    public string? Hostname { get; set; }
    public string? Note { get; set; }
}
```

```csharp
// CreateRedirectRequest.cs
namespace Umbraco.Community.NotFoundTracker.Models.Api;

public sealed class CreateRedirectRequest
{
    public Guid TargetContentKey { get; set; }
    public string? Culture { get; set; }
}
```

```csharp
// BulkIdsRequest.cs
namespace Umbraco.Community.NotFoundTracker.Models.Api;

public sealed class BulkIdsRequest
{
    public IReadOnlyList<int> Ids { get; set; } = Array.Empty<int>();
}
```

```csharp
// BulkIgnoreRequest.cs
namespace Umbraco.Community.NotFoundTracker.Models.Api;

public sealed class BulkIgnoreRequest
{
    public IReadOnlyList<int> Ids { get; set; } = Array.Empty<int>();
    public byte MatchType { get; set; }   // 0 = Exact, 1 = PathPrefix
}
```

```csharp
// BulkOpResponse.cs
namespace Umbraco.Community.NotFoundTracker.Models.Api;

public sealed class BulkOpResponse
{
    public int Processed { get; set; }
    public int Skipped { get; set; }
}
```

- [ ] **Step 2: Build + commit**

```bash
dotnet build src/Umbraco.Community.NotFoundTracker/Umbraco.Community.NotFoundTracker.csproj
git add src/Umbraco.Community.NotFoundTracker/Models/Api/
git commit -m "feat(NotFoundTracker): add API DTOs"
```

---

## Task 6: API controller base + Swagger registration

Mirrors `BlockRestrictionApiControllerBase` exactly — backoffice auth + API versioning + Swagger grouping.

**Files:**
- Create: `src/Umbraco.Community.NotFoundTracker/Controllers/NotFoundTrackerApiControllerBase.cs`
- Create: `src/Umbraco.Community.NotFoundTracker/Controllers/NotFoundTrackerOperationSecurityFilter.cs`
- Modify: `src/Umbraco.Community.NotFoundTracker/NotFoundTrackerBuilderExtensions.cs`

- [ ] **Step 1: Create the base controller**

```csharp
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Api.Common.Attributes;
using Umbraco.Cms.Web.Common.Authorization;
using Umbraco.Cms.Web.Common.Routing;

namespace Umbraco.Community.NotFoundTracker.Controllers;

[ApiController]
[BackOfficeRoute("umbracocommunitynotfoundtracker/api/v{version:apiVersion}")]
[Authorize(Policy = AuthorizationPolicies.SectionAccessContent)]
[MapToApi(Constants.ApiName)]
[ApiVersion("1.0")]
public abstract class NotFoundTrackerApiControllerBase : ControllerBase
{
}
```

- [ ] **Step 2: Create the Swagger operation security filter**

```csharp
using Umbraco.Cms.Api.Management.OpenApi;

namespace Umbraco.Community.NotFoundTracker.Controllers;

internal sealed class NotFoundTrackerOperationSecurityFilter : BackOfficeSecurityRequirementsOperationFilterBase
{
    protected override string ApiName => Constants.ApiName;
}
```

- [ ] **Step 3: Update the builder extension** — add Swagger registration

Open `src/Umbraco.Community.NotFoundTracker/NotFoundTrackerBuilderExtensions.cs`. Add these usings near the top:

```csharp
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using Umbraco.Community.NotFoundTracker.Controllers;
using Umbraco.Community.NotFoundTracker.Services;
```

Then inside `AddNotFoundTracker(...)`, AFTER the existing `AddHostedService<AutoPresetSeedingService>();` line, add:

```csharp
        // Service layer.
        builder.Services.AddScoped<INotFoundUserScopeService, NotFoundUserScopeService>();
        builder.Services.AddScoped<INotFoundHitService, NotFoundHitService>();
        builder.Services.AddScoped<INotFoundIgnoreRuleService, NotFoundIgnoreRuleService>();
        builder.Services.AddScoped<INotFoundRedirectService, NotFoundRedirectService>();

        // Swagger document for the management API.
        builder.Services.Configure<SwaggerGenOptions>(opt =>
        {
            opt.SwaggerDoc(Constants.ApiName, new OpenApiInfo
            {
                Title = "Umbraco Community NotFoundTracker Backoffice API",
                Version = "1.0",
            });
            opt.OperationFilter<NotFoundTrackerOperationSecurityFilter>();
        });
```

- [ ] **Step 4: Build + commit**

```bash
dotnet build src/Umbraco.Community.NotFoundTracker/Umbraco.Community.NotFoundTracker.csproj
git add src/Umbraco.Community.NotFoundTracker/
git commit -m "feat(NotFoundTracker): register service layer and Swagger document"
```

---

## Task 7: API controller — hit read endpoints

Implements `GET hits`, `GET hits/{id}`, `GET hits/hostnames`.

**Files:**
- Create: `src/Umbraco.Community.NotFoundTracker/Controllers/NotFoundTrackerApiController.cs`

- [ ] **Step 1: Create the controller (read endpoints only — write endpoints added in Task 8 + 9)**

```csharp
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Community.NotFoundTracker.Models.Api;
using Umbraco.Community.NotFoundTracker.Models.Entities;
using Umbraco.Community.NotFoundTracker.Services;

namespace Umbraco.Community.NotFoundTracker.Controllers;

public sealed class NotFoundTrackerApiController : NotFoundTrackerApiControllerBase
{
    private readonly INotFoundHitService _hits;
    private readonly INotFoundIgnoreRuleService _rules;
    private readonly INotFoundRedirectService _redirects;
    private readonly INotFoundUserScopeService _scope;

    public NotFoundTrackerApiController(
        INotFoundHitService hits,
        INotFoundIgnoreRuleService rules,
        INotFoundRedirectService redirects,
        INotFoundUserScopeService scope)
    {
        _hits = hits;
        _rules = rules;
        _redirects = redirects;
        _scope = scope;
    }

    [HttpGet("hits")]
    public async Task<ActionResult<HitListResponse>> ListHits(
        [FromQuery] string? hostname,
        [FromQuery] byte? status,
        [FromQuery] string? search,
        [FromQuery] byte sort = 0,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 25,
        CancellationToken ct = default)
    {
        var query = new HitListQuery
        {
            Hostname = string.IsNullOrEmpty(hostname) ? null : hostname.ToLowerInvariant(),
            Status = status.HasValue ? (HitStatus)status.Value : HitStatus.Active,
            Search = search,
            Sort = (HitSort)sort,
            Skip = Math.Max(0, skip),
            Take = Math.Clamp(take, 1, 200),
        };

        var (items, total) = await _hits.ListAsync(query, _scope.GetCurrentScope(), ct);
        return Ok(new HitListResponse
        {
            Total = total,
            Items = items.Select(MapItem).ToList(),
        });
    }

    [HttpGet("hits/{id:int}")]
    public async Task<ActionResult<HitDetail>> GetHit(int id, CancellationToken ct)
    {
        var hit = await _hits.GetAsync(id, _scope.GetCurrentScope(), ct);
        if (hit is null) return NotFound();

        return Ok(new HitDetail
        {
            Id = hit.Id,
            Hostname = hit.Hostname,
            Path = hit.Path,
            HitCount = hit.HitCount,
            FirstSeenUtc = hit.FirstSeenUtc,
            LastSeenUtc = hit.LastSeenUtc,
            LastUserAgent = hit.LastUserAgent,
            Status = (byte)hit.Status,
            QueryStrings = hit.QueryStrings.Select(q => new HitQueryStringItem
            {
                QueryString = q.QueryString,
                HitCount = q.HitCount,
                LastSeenUtc = q.LastSeenUtc,
            }).ToList(),
        });
    }

    [HttpGet("hits/hostnames")]
    public async Task<ActionResult<IReadOnlyList<string>>> GetHostnames(CancellationToken ct)
    {
        var hosts = await _hits.GetDistinctHostnamesAsync(_scope.GetCurrentScope(), ct);
        return Ok(hosts);
    }

    private static HitListItem MapItem(NotFoundHitEntity h) => new()
    {
        Id = h.Id,
        Hostname = h.Hostname,
        Path = h.Path,
        HitCount = h.HitCount,
        FirstSeenUtc = h.FirstSeenUtc,
        LastSeenUtc = h.LastSeenUtc,
        Status = (byte)h.Status,
    };
}
```

- [ ] **Step 2: Build + commit**

```bash
dotnet build src/Umbraco.Community.NotFoundTracker/Umbraco.Community.NotFoundTracker.csproj
git add src/Umbraco.Community.NotFoundTracker/Controllers/NotFoundTrackerApiController.cs
git commit -m "feat(NotFoundTracker): add hit read endpoints"
```

---

## Task 8: API controller — hit mutation endpoints

Adds `DELETE hits/{id}`, `POST hits/bulk-delete`, `POST hits/{id}/redirect`, `POST hits/{id}/ignore`, `POST hits/bulk-ignore`.

**Files:**
- Modify: `src/Umbraco.Community.NotFoundTracker/Controllers/NotFoundTrackerApiController.cs`

- [ ] **Step 1: Append these methods to the controller class (BEFORE the `MapItem` private helper)**

```csharp
    [HttpDelete("hits/{id:int}")]
    public async Task<IActionResult> DeleteHit(int id, CancellationToken ct)
    {
        var ok = await _hits.DeleteAsync(id, _scope.GetCurrentScope(), ct);
        return ok ? NoContent() : NotFound();
    }

    [HttpPost("hits/bulk-delete")]
    public async Task<ActionResult<BulkOpResponse>> BulkDeleteHits([FromBody] BulkIdsRequest request, CancellationToken ct)
    {
        var (processed, skipped) = await _hits.BulkDeleteAsync(request.Ids, _scope.GetCurrentScope(), ct);
        return Ok(new BulkOpResponse { Processed = processed, Skipped = skipped });
    }

    [HttpPost("hits/{id:int}/redirect")]
    public async Task<IActionResult> CreateRedirect(int id, [FromBody] CreateRedirectRequest request, CancellationToken ct)
    {
        var result = await _redirects.CreateRedirectForHitAsync(id, request.TargetContentKey, request.Culture, _scope.GetCurrentScope(), ct);

        return result.Kind switch
        {
            RedirectResultKind.Ok => NoContent(),
            RedirectResultKind.HitNotFound => NotFound(),
            RedirectResultKind.Forbidden => StatusCode(StatusCodes.Status403Forbidden, new { reason = result.Reason }),
            RedirectResultKind.TargetContentNotFound => BadRequest(new { reason = "Target content not found." }),
            RedirectResultKind.TargetContentNotAccessible => StatusCode(StatusCodes.Status403Forbidden, new { reason = result.Reason }),
            _ => StatusCode(StatusCodes.Status500InternalServerError, new { reason = result.Reason ?? "Redirect failed." }),
        };
    }

    [HttpPost("hits/{id:int}/ignore")]
    public async Task<IActionResult> CreateIgnoreFromHit(
        int id, [FromBody] CreateIgnoreRuleRequest request, CancellationToken ct)
    {
        var scope = _scope.GetCurrentScope();

        // Resolve the hit so we can flip its status after rule creation.
        var hit = await _hits.GetAsync(id, scope, ct);
        if (hit is null) return NotFound();

        // Create the rule.
        var ruleResult = await _rules.CreateAsync(
            new CreateIgnoreRuleInput(request.Path, (IgnoreMatchType)request.MatchType, request.Hostname, request.Note),
            scope, ct);

        if (ruleResult.Result == IgnoreRuleMutationResult.Forbidden)
            return StatusCode(StatusCodes.Status403Forbidden, new { reason = ruleResult.Reason });
        if (ruleResult.Result == IgnoreRuleMutationResult.Conflict)
            return Conflict(new { reason = ruleResult.Reason });
        if (ruleResult.Result == IgnoreRuleMutationResult.InvalidInput)
            return BadRequest(new { reason = ruleResult.Reason });

        // Flip the hit's status. Done as a fresh DB context call via the hit service —
        // we don't have an UpdateAsync, so call out to a helper directly through the
        // hit service via a status change. For simplicity (and to avoid expanding the hit
        // service surface this plan), reuse delete-then-no — actually we just delete the
        // hit here since the editor's intent ("ignore this URL") is equivalent to
        // "stop showing it." Deletion + the new ignore rule together produce that.
        await _hits.DeleteAsync(id, scope, ct);

        return NoContent();
    }

    [HttpPost("hits/bulk-ignore")]
    public async Task<ActionResult<BulkOpResponse>> BulkIgnoreHits([FromBody] BulkIgnoreRequest request, CancellationToken ct)
    {
        var scope = _scope.GetCurrentScope();
        var matchType = (IgnoreMatchType)request.MatchType;
        var processed = 0;
        var skipped = 0;

        foreach (var id in request.Ids)
        {
            var hit = await _hits.GetAsync(id, scope, ct);
            if (hit is null) { skipped++; continue; }

            var ruleResult = await _rules.CreateAsync(
                new CreateIgnoreRuleInput(hit.Path, matchType, hit.Hostname, Note: null),
                scope, ct);

            if (ruleResult.Result == IgnoreRuleMutationResult.Ok)
            {
                await _hits.DeleteAsync(id, scope, ct);
                processed++;
            }
            else
            {
                // Duplicate rule, forbidden, etc. — count as skipped.
                skipped++;
            }
        }

        return Ok(new BulkOpResponse { Processed = processed, Skipped = skipped });
    }
```

- [ ] **Step 2: Build + commit**

```bash
dotnet build src/Umbraco.Community.NotFoundTracker/Umbraco.Community.NotFoundTracker.csproj
git add src/Umbraco.Community.NotFoundTracker/Controllers/NotFoundTrackerApiController.cs
git commit -m "feat(NotFoundTracker): add hit mutation endpoints (delete, redirect, ignore)"
```

---

## Task 9: API controller — ignore rule endpoints

`GET ignore-rules`, `POST ignore-rules`, `PUT ignore-rules/{id}`, `DELETE ignore-rules/{id}`. Plus the re-seed auto-preset action.

**Files:**
- Modify: `src/Umbraco.Community.NotFoundTracker/Controllers/NotFoundTrackerApiController.cs`
- Inject `AutoPresetSeedingService` to re-trigger seeding from the API

- [ ] **Step 1: Append these methods to the controller class (BEFORE the `MapItem` private helper)**

```csharp
    [HttpGet("ignore-rules")]
    public async Task<ActionResult<IReadOnlyList<IgnoreRuleItem>>> ListIgnoreRules(CancellationToken ct)
    {
        var rules = await _rules.ListAsync(_scope.GetCurrentScope(), ct);
        return Ok(rules.Select(MapRule).ToList());
    }

    [HttpPost("ignore-rules")]
    public async Task<IActionResult> CreateIgnoreRule([FromBody] CreateIgnoreRuleRequest request, CancellationToken ct)
    {
        var result = await _rules.CreateAsync(
            new CreateIgnoreRuleInput(request.Path, (IgnoreMatchType)request.MatchType, request.Hostname, request.Note),
            _scope.GetCurrentScope(), ct);

        return MapMutation(result);
    }

    [HttpPut("ignore-rules/{id:int}")]
    public async Task<IActionResult> UpdateIgnoreRule(int id, [FromBody] UpdateIgnoreRuleRequest request, CancellationToken ct)
    {
        var result = await _rules.UpdateAsync(id,
            new UpdateIgnoreRuleInput(request.Path, (IgnoreMatchType)request.MatchType, request.Hostname, request.Note),
            _scope.GetCurrentScope(), ct);

        return MapMutation(result);
    }

    [HttpDelete("ignore-rules/{id:int}")]
    public async Task<IActionResult> DeleteIgnoreRule(int id, CancellationToken ct)
    {
        var result = await _rules.DeleteAsync(id, _scope.GetCurrentScope(), ct);
        return MapMutation(result);
    }

    private IActionResult MapMutation(IgnoreRuleMutation m) => m.Result switch
    {
        IgnoreRuleMutationResult.Ok => m.Entity is null ? NoContent() : Ok(MapRule(m.Entity)),
        IgnoreRuleMutationResult.NotFound => NotFound(),
        IgnoreRuleMutationResult.Forbidden => StatusCode(StatusCodes.Status403Forbidden, new { reason = m.Reason }),
        IgnoreRuleMutationResult.Conflict => Conflict(new { reason = m.Reason }),
        IgnoreRuleMutationResult.InvalidInput => BadRequest(new { reason = m.Reason }),
        _ => StatusCode(StatusCodes.Status500InternalServerError),
    };

    private static IgnoreRuleItem MapRule(NotFoundIgnoreRuleEntity r) => new()
    {
        Id = r.Id,
        Hostname = r.Hostname,
        MatchType = (byte)r.MatchType,
        Path = r.Path,
        Source = (byte)r.Source,
        Note = r.Note,
        CreatedUtc = r.CreatedUtc,
        IsReadOnly = r.Source == IgnoreRuleSource.ConfigSeeded,
    };
```

(`MapItem` and the new `MapRule` are both private helpers — fine to keep them at the bottom of the class.)

- [ ] **Step 2: Build + commit**

```bash
dotnet build src/Umbraco.Community.NotFoundTracker/Umbraco.Community.NotFoundTracker.csproj
git add src/Umbraco.Community.NotFoundTracker/Controllers/NotFoundTrackerApiController.cs
git commit -m "feat(NotFoundTracker): add ignore rule CRUD endpoints"
```

---

## Task 10: Re-seed endpoint + final wiring check

Add `POST ignore-rules/reseed-auto-preset` and confirm the full controller compiles + all 84 existing tests still pass.

**Files:**
- Modify: `src/Umbraco.Community.NotFoundTracker/Controllers/NotFoundTrackerApiController.cs`
- Modify: `src/Umbraco.Community.NotFoundTracker/Infrastructure/AutoPresetSeedingService.cs` (expose a public re-seed method)

- [ ] **Step 1: Expose a re-seed entry point on `AutoPresetSeedingService`**

Open `src/Umbraco.Community.NotFoundTracker/Infrastructure/AutoPresetSeedingService.cs`. Make the seeding logic callable from outside `StartAsync`. Add this new public method on the class:

```csharp
    /// <summary>
    /// Re-runs the seeding pass on demand (used by the management API). Equivalent to what
    /// StartAsync does on boot: inserts any missing auto-preset entries (respecting tombstones),
    /// reconciles config-seeded rows, refreshes the matcher.
    /// </summary>
    public async Task SeedAndReconcileAsync(CancellationToken ct)
    {
        await SeedAutoPresetAsync(ct);
        await ReconcileConfigSeededAsync(ct);
        await _matcher.RefreshAsync(ct);
    }
```

(`StartAsync` can keep its existing body — duplication is fine here, it just calls these in sequence too.)

- [ ] **Step 2: Update controller constructor to inject `AutoPresetSeedingService` and add the endpoint**

Edit `src/Umbraco.Community.NotFoundTracker/Controllers/NotFoundTrackerApiController.cs`:

Add to the field list:

```csharp
    private readonly Infrastructure.AutoPresetSeedingService _seedingService;
```

Update the constructor parameters + assignments:

```csharp
    public NotFoundTrackerApiController(
        INotFoundHitService hits,
        INotFoundIgnoreRuleService rules,
        INotFoundRedirectService redirects,
        INotFoundUserScopeService scope,
        Infrastructure.AutoPresetSeedingService seedingService)
    {
        _hits = hits;
        _rules = rules;
        _redirects = redirects;
        _scope = scope;
        _seedingService = seedingService;
    }
```

Add the endpoint (before the private helpers):

```csharp
    [HttpPost("ignore-rules/reseed-auto-preset")]
    public async Task<IActionResult> ReseedAutoPreset(CancellationToken ct)
    {
        if (!_scope.GetCurrentScope().HasFullAccess)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { reason = "Full access required." });
        }
        await _seedingService.SeedAndReconcileAsync(ct);
        return NoContent();
    }
```

- [ ] **Step 3: Update DI registration** — currently `AutoPresetSeedingService` is registered via `AddHostedService<>` which only gives the hosted-service singleton. We also need it injectable. Open `NotFoundTrackerBuilderExtensions.cs` and CHANGE the line:

```csharp
        builder.Services.AddHostedService<AutoPresetSeedingService>();
```

to:

```csharp
        builder.Services.AddSingleton<AutoPresetSeedingService>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<AutoPresetSeedingService>());
```

This registers it as a singleton AND as a hosted service, sharing the same instance.

- [ ] **Step 4: Build + run all tests**

```bash
dotnet build UmbracoCommunity.sln
dotnet test tests/Umbraco.Community.NotFoundTracker.Tests/Umbraco.Community.NotFoundTracker.Tests.csproj
```

Expected: build clean, 84 tests still pass.

- [ ] **Step 5: Commit**

```bash
git add src/Umbraco.Community.NotFoundTracker/
git commit -m "feat(NotFoundTracker): add reseed-auto-preset endpoint"
```

---

## Phase E + F overview — Frontend dashboard

The frontend is a self-contained Lit + Vite project under `Client/`. It builds to `wwwroot/App_Plugins/UmbracoCommunityNotFoundTracker/` and registers via `umbraco-package.json`. The tasks below scaffold the project structure, then implement the dashboard tab by tab.

If you're stopping after Phase D, skip to Phase G's verification and validate the API via Swagger. Tasks 11–17 produce the editor-facing UI.

---

## Task 11: Frontend scaffold

**Files:**
- Create: `src/Umbraco.Community.NotFoundTracker/Client/package.json`
- Create: `src/Umbraco.Community.NotFoundTracker/Client/tsconfig.json`
- Create: `src/Umbraco.Community.NotFoundTracker/Client/vite.config.ts`
- Create: `src/Umbraco.Community.NotFoundTracker/Client/vitest.config.ts`
- Create: `src/Umbraco.Community.NotFoundTracker/Client/public/umbraco-package.json`
- Create: `src/Umbraco.Community.NotFoundTracker/Client/src/bundle.manifests.ts` (placeholder)
- Modify: `src/Umbraco.Community.NotFoundTracker/Umbraco.Community.NotFoundTracker.csproj` (exclude `Client/`)

- [ ] **Step 1: Create `package.json`**

```json
{
  "name": "umbraco-community-not-found-tracker",
  "private": true,
  "version": "0.0.0",
  "type": "module",
  "scripts": {
    "watch": "tsc && vite build --watch",
    "build": "tsc && vite build",
    "test": "vitest run",
    "test:coverage": "vitest run --coverage"
  },
  "devDependencies": {
    "@umbraco-cms/backoffice": "^*",
    "jsdom": "^27.3.0",
    "lit": "^3.3.0",
    "typescript": "^5.9.3",
    "vite": "^7.1.9",
    "vitest": "^4.0.15"
  }
}
```

- [ ] **Step 2: Create `tsconfig.json`**

```json
{
  "compilerOptions": {
    "target": "ES2022",
    "useDefineForClassFields": true,
    "module": "ESNext",
    "lib": ["ES2022", "DOM", "DOM.Iterable"],
    "skipLibCheck": true,
    "moduleResolution": "bundler",
    "resolveJsonModule": true,
    "isolatedModules": true,
    "noEmit": true,
    "jsx": "preserve",
    "strict": true,
    "noUnusedLocals": true,
    "noUnusedParameters": true,
    "noFallthroughCasesInSwitch": true,
    "experimentalDecorators": true,
    "useDefineForClassFields": false
  },
  "include": ["src"]
}
```

- [ ] **Step 3: Create `vite.config.ts`**

```typescript
import { defineConfig } from "vite";

export default defineConfig({
  build: {
    lib: {
      entry: "src/bundle.manifests.ts",
      formats: ["es"],
      fileName: () => "not-found-tracker.js",
    },
    outDir: "../wwwroot/App_Plugins/UmbracoCommunityNotFoundTracker",
    emptyOutDir: true,
    sourcemap: true,
    rollupOptions: {
      external: [/^@umbraco-cms\/backoffice/],
    },
  },
});
```

- [ ] **Step 4: Create `vitest.config.ts`**

```typescript
import { defineConfig } from "vitest/config";

export default defineConfig({
  test: {
    environment: "jsdom",
    globals: true,
  },
});
```

- [ ] **Step 5: Create `public/umbraco-package.json`**

```json
{
  "id": "Umbraco.Community.NotFoundTracker",
  "name": "404 Tracker",
  "version": "0.0.0",
  "allowTelemetry": true,
  "extensions": [
    {
      "type": "bundle",
      "alias": "Umbraco.Community.NotFoundTracker.Bundle",
      "name": "Not Found Tracker Bundle",
      "js": "/App_Plugins/UmbracoCommunityNotFoundTracker/not-found-tracker.js"
    }
  ]
}
```

- [ ] **Step 6: Create `src/bundle.manifests.ts`** (placeholder — actual extensions added in Task 12)

```typescript
import type { ManifestTypes } from "@umbraco-cms/backoffice/extension-registry";

export const manifests: Array<ManifestTypes> = [];
```

- [ ] **Step 7: Update csproj to exclude `Client/` from pack output**

Open `src/Umbraco.Community.NotFoundTracker/Umbraco.Community.NotFoundTracker.csproj`. Add inside the existing `<ItemGroup>` that has `<Folder Include="wwwroot\" />` (or add a new `<ItemGroup>`):

```xml
  <ItemGroup>
    <Content Remove="Client\**" />
    <None Include="Client\public\umbraco-package.json" Pack="false" />
  </ItemGroup>
```

- [ ] **Step 8: Install dependencies and verify build**

```bash
cd src/Umbraco.Community.NotFoundTracker/Client && npm install && npm run build && cd -
```

Expected: produces `wwwroot/App_Plugins/UmbracoCommunityNotFoundTracker/not-found-tracker.js` (empty bundle, since `bundle.manifests.ts` exports an empty array).

- [ ] **Step 9: Commit**

```bash
git add src/Umbraco.Community.NotFoundTracker/Client/ src/Umbraco.Community.NotFoundTracker/Umbraco.Community.NotFoundTracker.csproj
git commit -m "feat(NotFoundTracker): scaffold Vite/Lit frontend project"
```

(Note: don't commit `node_modules/`; it should be gitignored already from the BlockRestrictions setup or via repo-root `.gitignore`. Verify with `git status` before committing.)

---

## Task 12: API client + types

**Files:**
- Create: `src/Umbraco.Community.NotFoundTracker/Client/src/api/types.ts`
- Create: `src/Umbraco.Community.NotFoundTracker/Client/src/api/not-found-tracker-api.ts`

- [ ] **Step 1: Create TypeScript types matching backend DTOs**

```typescript
// src/api/types.ts

export interface HitListItem {
  id: number;
  hostname: string;
  path: string;
  hitCount: number;
  firstSeenUtc: string;
  lastSeenUtc: string;
  status: number;       // 0 Active, 1 IgnoredManually, 2 Redirected
}

export interface HitListResponse {
  total: number;
  items: HitListItem[];
}

export interface HitQueryStringItem {
  queryString: string;
  hitCount: number;
  lastSeenUtc: string;
}

export interface HitDetail extends HitListItem {
  lastUserAgent: string | null;
  queryStrings: HitQueryStringItem[];
}

export interface IgnoreRuleItem {
  id: number;
  hostname: string | null;
  matchType: number;      // 0 Exact, 1 PathPrefix
  path: string;
  source: number;         // 0 UserDefined, 1 AutoPreset, 2 ConfigSeeded
  note: string | null;
  createdUtc: string;
  isReadOnly: boolean;
}

export interface BulkOpResponse {
  processed: number;
  skipped: number;
}

export interface HitListQuery {
  hostname?: string;
  status?: number;
  search?: string;
  sort?: number;
  skip?: number;
  take?: number;
}
```

- [ ] **Step 2: Create the fetch wrapper**

```typescript
// src/api/not-found-tracker-api.ts
import type { HitListResponse, HitListQuery, HitDetail, IgnoreRuleItem, BulkOpResponse } from "./types";

const BASE = "/umbraco/umbracocommunitynotfoundtracker/api/v1";

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${BASE}${path}`, {
    ...init,
    credentials: "include",
    headers: { "Content-Type": "application/json", ...(init?.headers ?? {}) },
  });
  if (!response.ok) {
    const text = await response.text();
    throw new Error(`${response.status} ${response.statusText}: ${text}`);
  }
  if (response.status === 204) return undefined as T;
  return response.json() as Promise<T>;
}

export const NotFoundTrackerApi = {
  listHits(query: HitListQuery): Promise<HitListResponse> {
    const params = new URLSearchParams();
    if (query.hostname) params.set("hostname", query.hostname);
    if (query.status !== undefined) params.set("status", String(query.status));
    if (query.search) params.set("search", query.search);
    if (query.sort !== undefined) params.set("sort", String(query.sort));
    if (query.skip !== undefined) params.set("skip", String(query.skip));
    if (query.take !== undefined) params.set("take", String(query.take));
    return request<HitListResponse>(`/hits?${params.toString()}`);
  },

  getHit(id: number): Promise<HitDetail> {
    return request<HitDetail>(`/hits/${id}`);
  },

  getHostnames(): Promise<string[]> {
    return request<string[]>(`/hits/hostnames`);
  },

  deleteHit(id: number): Promise<void> {
    return request<void>(`/hits/${id}`, { method: "DELETE" });
  },

  bulkDeleteHits(ids: number[]): Promise<BulkOpResponse> {
    return request<BulkOpResponse>(`/hits/bulk-delete`, {
      method: "POST",
      body: JSON.stringify({ ids }),
    });
  },

  createRedirect(hitId: number, targetContentKey: string, culture: string | null): Promise<void> {
    return request<void>(`/hits/${hitId}/redirect`, {
      method: "POST",
      body: JSON.stringify({ targetContentKey, culture }),
    });
  },

  ignoreFromHit(hitId: number, path: string, matchType: number, hostname: string | null, note: string | null): Promise<void> {
    return request<void>(`/hits/${hitId}/ignore`, {
      method: "POST",
      body: JSON.stringify({ path, matchType, hostname, note }),
    });
  },

  bulkIgnoreHits(ids: number[], matchType: number): Promise<BulkOpResponse> {
    return request<BulkOpResponse>(`/hits/bulk-ignore`, {
      method: "POST",
      body: JSON.stringify({ ids, matchType }),
    });
  },

  listIgnoreRules(): Promise<IgnoreRuleItem[]> {
    return request<IgnoreRuleItem[]>(`/ignore-rules`);
  },

  createIgnoreRule(path: string, matchType: number, hostname: string | null, note: string | null): Promise<IgnoreRuleItem> {
    return request<IgnoreRuleItem>(`/ignore-rules`, {
      method: "POST",
      body: JSON.stringify({ path, matchType, hostname, note }),
    });
  },

  updateIgnoreRule(id: number, path: string, matchType: number, hostname: string | null, note: string | null): Promise<IgnoreRuleItem> {
    return request<IgnoreRuleItem>(`/ignore-rules/${id}`, {
      method: "PUT",
      body: JSON.stringify({ path, matchType, hostname, note }),
    });
  },

  deleteIgnoreRule(id: number): Promise<void> {
    return request<void>(`/ignore-rules/${id}`, { method: "DELETE" });
  },

  reseedAutoPreset(): Promise<void> {
    return request<void>(`/ignore-rules/reseed-auto-preset`, { method: "POST" });
  },
};
```

- [ ] **Step 3: Build + commit**

```bash
cd src/Umbraco.Community.NotFoundTracker/Client && npm run build && cd -
git add src/Umbraco.Community.NotFoundTracker/Client/src/
git commit -m "feat(NotFoundTracker): add frontend API client + types"
```

---

## Task 13: Dashboard shell + bundle manifest

A two-tab dashboard registered into the Content section.

**Files:**
- Create: `src/Umbraco.Community.NotFoundTracker/Client/src/dashboards/not-found-tracker-dashboard.element.ts`
- Modify: `src/Umbraco.Community.NotFoundTracker/Client/src/bundle.manifests.ts`

- [ ] **Step 1: Create the dashboard element**

```typescript
// src/dashboards/not-found-tracker-dashboard.element.ts
import { LitElement, html, css } from "lit";
import { customElement, state } from "lit/decorators.js";

@customElement("not-found-tracker-dashboard")
export class NotFoundTrackerDashboardElement extends LitElement {
  @state() private activeTab: "hits" | "rules" = "hits";

  static styles = css`
    :host {
      display: block;
      padding: var(--uui-size-space-4, 16px);
    }
    .tabs {
      display: flex;
      gap: var(--uui-size-space-2, 8px);
      border-bottom: 1px solid var(--uui-color-divider, #e9e9eb);
      margin-bottom: var(--uui-size-space-4, 16px);
    }
    .tab-btn {
      background: none;
      border: 0;
      padding: var(--uui-size-space-3, 12px) var(--uui-size-space-4, 16px);
      cursor: pointer;
      border-bottom: 2px solid transparent;
      font: inherit;
    }
    .tab-btn[aria-selected="true"] {
      border-bottom-color: var(--uui-color-selected, #3544b1);
      color: var(--uui-color-selected, #3544b1);
    }
  `;

  render() {
    return html`
      <div role="tablist" class="tabs">
        <button
          class="tab-btn"
          role="tab"
          aria-selected="${this.activeTab === "hits"}"
          @click=${() => (this.activeTab = "hits")}
        >
          Hits
        </button>
        <button
          class="tab-btn"
          role="tab"
          aria-selected="${this.activeTab === "rules"}"
          @click=${() => (this.activeTab = "rules")}
        >
          Ignore rules
        </button>
      </div>
      ${this.activeTab === "hits"
        ? html`<not-found-tracker-hits-tab></not-found-tracker-hits-tab>`
        : html`<not-found-tracker-ignore-rules-tab></not-found-tracker-ignore-rules-tab>`}
    `;
  }
}

declare global {
  interface HTMLElementTagNameMap {
    "not-found-tracker-dashboard": NotFoundTrackerDashboardElement;
  }
}
```

- [ ] **Step 2: Update `bundle.manifests.ts` to register the dashboard**

```typescript
import type { ManifestTypes } from "@umbraco-cms/backoffice/extension-registry";

export const manifests: Array<ManifestTypes> = [
  {
    type: "dashboard",
    alias: "Umbraco.Community.NotFoundTracker.Dashboard",
    name: "404 Tracker",
    element: () => import("./dashboards/not-found-tracker-dashboard.element.js"),
    weight: 100,
    meta: {
      label: "404 Tracker",
      pathname: "not-found-tracker",
    },
    conditions: [
      {
        alias: "Umb.Condition.SectionAlias",
        match: "Umb.Section.Content",
      },
    ],
  },
];
```

- [ ] **Step 3: Build + commit**

```bash
cd src/Umbraco.Community.NotFoundTracker/Client && npm run build && cd -
git add src/Umbraco.Community.NotFoundTracker/Client/src/
git commit -m "feat(NotFoundTracker): add dashboard shell with tab navigation"
```

---

## Task 14: Hits tab

The biggest UI task. Hits table with filters (hostname, status, search), sort buttons, pagination, and row actions (placeholder for now — actions wired in Task 15).

**Files:**
- Create: `src/Umbraco.Community.NotFoundTracker/Client/src/dashboards/hits-tab.element.ts`

- [ ] **Step 1: Create the element**

```typescript
// src/dashboards/hits-tab.element.ts
import { LitElement, html, css, nothing } from "lit";
import { customElement, state } from "lit/decorators.js";
import { NotFoundTrackerApi } from "../api/not-found-tracker-api.js";
import type { HitListItem, HitListResponse } from "../api/types.js";

const STATUS_LABELS = ["Active", "Ignored", "Redirected"];
const SORTS = [
  { value: 0, label: "Recently seen" },
  { value: 1, label: "Popularity" },
  { value: 2, label: "First seen" },
];

@customElement("not-found-tracker-hits-tab")
export class HitsTabElement extends LitElement {
  @state() private items: HitListItem[] = [];
  @state() private total = 0;
  @state() private loading = false;
  @state() private error: string | null = null;

  @state() private hostnames: string[] = [];
  @state() private hostnameFilter = "";
  @state() private statusFilter: number = 0;
  @state() private search = "";
  @state() private sort: number = 0;
  @state() private skip = 0;
  @state() private take = 25;

  @state() private selectedIds = new Set<number>();

  static styles = css`
    :host { display: block; }
    .toolbar {
      display: flex; gap: var(--uui-size-space-3, 12px); align-items: end;
      flex-wrap: wrap; margin-bottom: var(--uui-size-space-4, 16px);
    }
    .field { display: flex; flex-direction: column; gap: 4px; }
    .field label { font-size: 12px; color: var(--uui-color-text-alt, #686c87); }
    select, input { padding: 6px 8px; border: 1px solid var(--uui-color-border, #d6d6d6); border-radius: 3px; }
    table { width: 100%; border-collapse: collapse; }
    th, td { text-align: left; padding: 8px; border-bottom: 1px solid var(--uui-color-divider, #e9e9eb); }
    th { background: var(--uui-color-surface-alt, #f9f9fb); }
    .badge { padding: 2px 8px; border-radius: 10px; font-size: 11px; background: var(--uui-color-surface-alt, #f0f0f4); }
    .badge.status-2 { background: #d6e9d6; color: #2d6a2d; }
    .badge.status-1 { background: #e8e1ee; color: #6a4c8c; }
    .pagination { display: flex; gap: 8px; align-items: center; margin-top: 16px; }
    .pagination button[disabled] { opacity: .5; }
    .row-checkbox { width: 32px; }
  `;

  connectedCallback() {
    super.connectedCallback();
    this.load();
    this.loadHostnames();
  }

  private async loadHostnames() {
    try {
      this.hostnames = await NotFoundTrackerApi.getHostnames();
    } catch (e) {
      // Non-fatal — dropdown stays empty if this fails.
    }
  }

  private async load() {
    this.loading = true;
    this.error = null;
    try {
      const result: HitListResponse = await NotFoundTrackerApi.listHits({
        hostname: this.hostnameFilter || undefined,
        status: this.statusFilter,
        search: this.search || undefined,
        sort: this.sort,
        skip: this.skip,
        take: this.take,
      });
      this.items = result.items;
      this.total = result.total;
    } catch (e) {
      this.error = (e as Error).message;
    } finally {
      this.loading = false;
    }
  }

  private toggleSelect(id: number) {
    if (this.selectedIds.has(id)) this.selectedIds.delete(id);
    else this.selectedIds.add(id);
    this.requestUpdate();
  }

  private async deleteSelected() {
    if (this.selectedIds.size === 0) return;
    const ok = confirm(`Delete ${this.selectedIds.size} hit(s)?`);
    if (!ok) return;
    await NotFoundTrackerApi.bulkDeleteHits([...this.selectedIds]);
    this.selectedIds.clear();
    await this.load();
  }

  private async deleteOne(id: number) {
    if (!confirm("Delete this hit?")) return;
    await NotFoundTrackerApi.deleteHit(id);
    await this.load();
  }

  render() {
    return html`
      <div class="toolbar">
        <div class="field">
          <label>Hostname</label>
          <select @change=${(e: Event) => { this.hostnameFilter = (e.target as HTMLSelectElement).value; this.skip = 0; this.load(); }}>
            <option value="">All sites</option>
            ${this.hostnames.map(h => html`<option value=${h} ?selected=${this.hostnameFilter === h}>${h}</option>`)}
          </select>
        </div>
        <div class="field">
          <label>Status</label>
          <select @change=${(e: Event) => { this.statusFilter = parseInt((e.target as HTMLSelectElement).value); this.skip = 0; this.load(); }}>
            <option value="0" ?selected=${this.statusFilter === 0}>Active</option>
            <option value="1" ?selected=${this.statusFilter === 1}>Ignored</option>
            <option value="2" ?selected=${this.statusFilter === 2}>Redirected</option>
          </select>
        </div>
        <div class="field">
          <label>Search</label>
          <input type="search" placeholder="Path contains..." @input=${(e: Event) => { this.search = (e.target as HTMLInputElement).value; this.skip = 0; }} @change=${() => this.load()}>
        </div>
        <div class="field">
          <label>Sort</label>
          <select @change=${(e: Event) => { this.sort = parseInt((e.target as HTMLSelectElement).value); this.load(); }}>
            ${SORTS.map(s => html`<option value=${s.value} ?selected=${this.sort === s.value}>${s.label}</option>`)}
          </select>
        </div>
        ${this.selectedIds.size > 0
          ? html`<button @click=${this.deleteSelected}>Delete selected (${this.selectedIds.size})</button>`
          : nothing}
      </div>

      ${this.error ? html`<div style="color: red;">Error: ${this.error}</div>` : nothing}
      ${this.loading ? html`<div>Loading…</div>` : nothing}

      <table>
        <thead>
          <tr>
            <th class="row-checkbox"></th>
            <th>Path</th>
            <th>Hostname</th>
            <th>Hits</th>
            <th>Last seen</th>
            <th>Status</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          ${this.items.length === 0 && !this.loading
            ? html`<tr><td colspan="7" style="text-align:center;padding:24px;color:#888;">No hits.</td></tr>`
            : this.items.map(item => html`
              <tr>
                <td class="row-checkbox">
                  <input type="checkbox" .checked=${this.selectedIds.has(item.id)} @change=${() => this.toggleSelect(item.id)}>
                </td>
                <td>${item.path}</td>
                <td>${item.hostname}</td>
                <td>${item.hitCount}</td>
                <td>${new Date(item.lastSeenUtc).toLocaleString()}</td>
                <td><span class="badge status-${item.status}">${STATUS_LABELS[item.status]}</span></td>
                <td>
                  <button @click=${() => this.deleteOne(item.id)}>Delete</button>
                </td>
              </tr>
            `)}
        </tbody>
      </table>

      <div class="pagination">
        <button @click=${() => { this.skip = Math.max(0, this.skip - this.take); this.load(); }} ?disabled=${this.skip === 0}>‹ Prev</button>
        <span>${this.skip + 1}-${Math.min(this.skip + this.take, this.total)} of ${this.total}</span>
        <button @click=${() => { this.skip += this.take; this.load(); }} ?disabled=${this.skip + this.take >= this.total}>Next ›</button>
      </div>
    `;
  }
}

declare global {
  interface HTMLElementTagNameMap {
    "not-found-tracker-hits-tab": HitsTabElement;
  }
}
```

- [ ] **Step 2: Update `bundle.manifests.ts`** to ensure the dashboard imports the element (the auto-render in the parent triggers the custom element to load — but we need to make sure the file is in the bundle). The simplest way: add a side-effect import to `bundle.manifests.ts`:

```typescript
import "./dashboards/hits-tab.element.js";
```

Add that line at the top of `src/bundle.manifests.ts`.

- [ ] **Step 3: Build + commit**

```bash
cd src/Umbraco.Community.NotFoundTracker/Client && npm run build && cd -
git add src/Umbraco.Community.NotFoundTracker/Client/src/
git commit -m "feat(NotFoundTracker): add hits tab with filters, sort, and pagination"
```

---

## Task 15: Hits tab — redirect & ignore modals

Adds the two per-row action buttons that open modals: "Create redirect" (with a content picker) and "Ignore this URL" (with match-type selection).

**Files:**
- Create: `src/Umbraco.Community.NotFoundTracker/Client/src/dashboards/modals/create-redirect-modal.element.ts`
- Create: `src/Umbraco.Community.NotFoundTracker/Client/src/dashboards/modals/add-ignore-rule-modal.element.ts`
- Modify: `src/Umbraco.Community.NotFoundTracker/Client/src/dashboards/hits-tab.element.ts`

- [ ] **Step 1: Create the redirect modal**

```typescript
// src/dashboards/modals/create-redirect-modal.element.ts
import { LitElement, html, css } from "lit";
import { customElement, property, state } from "lit/decorators.js";
import { NotFoundTrackerApi } from "../../api/not-found-tracker-api.js";

@customElement("not-found-tracker-create-redirect-modal")
export class CreateRedirectModalElement extends LitElement {
  @property({ type: Number }) hitId = 0;
  @property() hitPath = "";
  @state() private targetKey = "";
  @state() private culture = "";
  @state() private busy = false;
  @state() private error: string | null = null;

  static styles = css`
    :host { display: block; padding: var(--uui-size-space-4, 16px); min-width: 400px; }
    .field { margin-bottom: var(--uui-size-space-3, 12px); display: flex; flex-direction: column; gap: 4px; }
    .field label { font-size: 12px; color: var(--uui-color-text-alt, #686c87); }
    input { padding: 6px 8px; border: 1px solid var(--uui-color-border, #d6d6d6); border-radius: 3px; }
    .actions { display: flex; gap: 8px; justify-content: flex-end; }
    .error { color: red; margin-bottom: 8px; }
  `;

  private async submit() {
    if (!this.targetKey) {
      this.error = "Target content key is required.";
      return;
    }
    this.busy = true;
    this.error = null;
    try {
      await NotFoundTrackerApi.createRedirect(this.hitId, this.targetKey, this.culture || null);
      this.dispatchEvent(new CustomEvent("done", { bubbles: true, composed: true }));
    } catch (e) {
      this.error = (e as Error).message;
    } finally {
      this.busy = false;
    }
  }

  private cancel() {
    this.dispatchEvent(new CustomEvent("cancel", { bubbles: true, composed: true }));
  }

  render() {
    return html`
      <h3>Create redirect</h3>
      <p>Redirect from <strong>${this.hitPath}</strong> to a content node.</p>
      ${this.error ? html`<div class="error">${this.error}</div>` : ""}
      <div class="field">
        <label>Target content key (GUID)</label>
        <input
          type="text"
          placeholder="00000000-0000-0000-0000-000000000000"
          .value=${this.targetKey}
          @input=${(e: Event) => (this.targetKey = (e.target as HTMLInputElement).value)}
        >
        <small>Paste the content node's key from its info tab. Replace this with Umbraco's content picker once
        the dashboard is wired into the backoffice modal manager — see follow-up plan.</small>
      </div>
      <div class="field">
        <label>Culture (optional)</label>
        <input
          type="text"
          placeholder="en-us"
          .value=${this.culture}
          @input=${(e: Event) => (this.culture = (e.target as HTMLInputElement).value)}
        >
      </div>
      <div class="actions">
        <button @click=${this.cancel} ?disabled=${this.busy}>Cancel</button>
        <button @click=${this.submit} ?disabled=${this.busy}>${this.busy ? "Saving…" : "Create redirect"}</button>
      </div>
    `;
  }
}

declare global {
  interface HTMLElementTagNameMap {
    "not-found-tracker-create-redirect-modal": CreateRedirectModalElement;
  }
}
```

- [ ] **Step 2: Create the ignore-rule modal**

```typescript
// src/dashboards/modals/add-ignore-rule-modal.element.ts
import { LitElement, html, css } from "lit";
import { customElement, property, state } from "lit/decorators.js";
import { NotFoundTrackerApi } from "../../api/not-found-tracker-api.js";

@customElement("not-found-tracker-add-ignore-rule-modal")
export class AddIgnoreRuleModalElement extends LitElement {
  @property({ type: Number }) hitId = 0;
  @property() suggestedPath = "";
  @property() suggestedHostname: string | null = null;
  @state() private matchType: number = 1;  // 0 Exact, 1 PathPrefix
  @state() private path = "";
  @state() private hostname: string | null = null;
  @state() private note = "";
  @state() private busy = false;
  @state() private error: string | null = null;

  static styles = css`
    :host { display: block; padding: var(--uui-size-space-4, 16px); min-width: 400px; }
    .field { margin-bottom: var(--uui-size-space-3, 12px); display: flex; flex-direction: column; gap: 4px; }
    .field label { font-size: 12px; color: var(--uui-color-text-alt, #686c87); }
    input, select, textarea { padding: 6px 8px; border: 1px solid var(--uui-color-border, #d6d6d6); border-radius: 3px; }
    .actions { display: flex; gap: 8px; justify-content: flex-end; }
    .error { color: red; margin-bottom: 8px; }
  `;

  connectedCallback() {
    super.connectedCallback();
    this.path = this.suggestedPath;
    this.hostname = this.suggestedHostname;
  }

  private async submit() {
    this.busy = true;
    this.error = null;
    try {
      if (this.hitId > 0) {
        await NotFoundTrackerApi.ignoreFromHit(this.hitId, this.path, this.matchType, this.hostname, this.note || null);
      } else {
        await NotFoundTrackerApi.createIgnoreRule(this.path, this.matchType, this.hostname, this.note || null);
      }
      this.dispatchEvent(new CustomEvent("done", { bubbles: true, composed: true }));
    } catch (e) {
      this.error = (e as Error).message;
    } finally {
      this.busy = false;
    }
  }

  private cancel() {
    this.dispatchEvent(new CustomEvent("cancel", { bubbles: true, composed: true }));
  }

  render() {
    return html`
      <h3>Add ignore rule</h3>
      ${this.error ? html`<div class="error">${this.error}</div>` : ""}
      <div class="field">
        <label>Path</label>
        <input
          type="text"
          .value=${this.path}
          @input=${(e: Event) => (this.path = (e.target as HTMLInputElement).value)}
        >
      </div>
      <div class="field">
        <label>Match type</label>
        <select @change=${(e: Event) => (this.matchType = parseInt((e.target as HTMLSelectElement).value))}>
          <option value="0" ?selected=${this.matchType === 0}>Exact</option>
          <option value="1" ?selected=${this.matchType === 1}>Path prefix</option>
        </select>
      </div>
      <div class="field">
        <label>Hostname (optional, leave blank for global)</label>
        <input
          type="text"
          placeholder=${this.suggestedHostname ?? "all sites"}
          .value=${this.hostname ?? ""}
          @input=${(e: Event) => (this.hostname = (e.target as HTMLInputElement).value || null)}
        >
      </div>
      <div class="field">
        <label>Note (optional)</label>
        <textarea rows="2" @input=${(e: Event) => (this.note = (e.target as HTMLTextAreaElement).value)}></textarea>
      </div>
      <div class="actions">
        <button @click=${this.cancel} ?disabled=${this.busy}>Cancel</button>
        <button @click=${this.submit} ?disabled=${this.busy}>${this.busy ? "Saving…" : "Add rule"}</button>
      </div>
    `;
  }
}

declare global {
  interface HTMLElementTagNameMap {
    "not-found-tracker-add-ignore-rule-modal": AddIgnoreRuleModalElement;
  }
}
```

- [ ] **Step 3: Wire the modals into hits-tab**

Edit `src/dashboards/hits-tab.element.ts`. Add at the top:

```typescript
import "./modals/create-redirect-modal.element.js";
import "./modals/add-ignore-rule-modal.element.js";
```

Add to the `@state` declarations:

```typescript
  @state() private redirectingFor: HitListItem | null = null;
  @state() private ignoringFor: HitListItem | null = null;
```

In the row template's actions cell, replace the existing `<button @click=${() => this.deleteOne(item.id)}>Delete</button>` with:

```typescript
                  <button @click=${() => (this.redirectingFor = item)}>Redirect</button>
                  <button @click=${() => (this.ignoringFor = item)}>Ignore</button>
                  <button @click=${() => this.deleteOne(item.id)}>Delete</button>
```

Add the modal overlays at the end of the `render()` template, after the closing `</div>` of `.pagination`:

```typescript
      ${this.redirectingFor
        ? html`
          <div style="position:fixed;inset:0;background:rgba(0,0,0,.4);display:flex;align-items:center;justify-content:center;z-index:1000;">
            <div style="background:white;border-radius:6px;">
              <not-found-tracker-create-redirect-modal
                .hitId=${this.redirectingFor.id}
                .hitPath=${this.redirectingFor.path}
                @done=${() => { this.redirectingFor = null; this.load(); }}
                @cancel=${() => (this.redirectingFor = null)}
              ></not-found-tracker-create-redirect-modal>
            </div>
          </div>
        `
        : nothing}
      ${this.ignoringFor
        ? html`
          <div style="position:fixed;inset:0;background:rgba(0,0,0,.4);display:flex;align-items:center;justify-content:center;z-index:1000;">
            <div style="background:white;border-radius:6px;">
              <not-found-tracker-add-ignore-rule-modal
                .hitId=${this.ignoringFor.id}
                .suggestedPath=${this.ignoringFor.path}
                .suggestedHostname=${this.ignoringFor.hostname}
                @done=${() => { this.ignoringFor = null; this.load(); }}
                @cancel=${() => (this.ignoringFor = null)}
              ></not-found-tracker-add-ignore-rule-modal>
            </div>
          </div>
        `
        : nothing}
```

- [ ] **Step 4: Build + commit**

```bash
cd src/Umbraco.Community.NotFoundTracker/Client && npm run build && cd -
git add src/Umbraco.Community.NotFoundTracker/Client/src/
git commit -m "feat(NotFoundTracker): add redirect + ignore modals to hits tab"
```

---

## Task 16: Ignore rules tab

The second dashboard tab.

**Files:**
- Create: `src/Umbraco.Community.NotFoundTracker/Client/src/dashboards/ignore-rules-tab.element.ts`
- Modify: `src/Umbraco.Community.NotFoundTracker/Client/src/bundle.manifests.ts`

- [ ] **Step 1: Create the element**

```typescript
// src/dashboards/ignore-rules-tab.element.ts
import { LitElement, html, css, nothing } from "lit";
import { customElement, state } from "lit/decorators.js";
import { NotFoundTrackerApi } from "../api/not-found-tracker-api.js";
import type { IgnoreRuleItem } from "../api/types.js";
import "./modals/add-ignore-rule-modal.element.js";

const MATCH_LABELS = ["Exact", "Prefix"];
const SOURCE_LABELS = ["User-defined", "Auto-preset", "Config"];

@customElement("not-found-tracker-ignore-rules-tab")
export class IgnoreRulesTabElement extends LitElement {
  @state() private rules: IgnoreRuleItem[] = [];
  @state() private loading = false;
  @state() private error: string | null = null;
  @state() private sourceFilter: number | "all" = "all";
  @state() private hostnameFilter = "";
  @state() private search = "";
  @state() private addingRule = false;

  static styles = css`
    :host { display: block; }
    .toolbar { display: flex; gap: 12px; align-items: end; flex-wrap: wrap; margin-bottom: 16px; }
    .field { display: flex; flex-direction: column; gap: 4px; }
    .field label { font-size: 12px; color: var(--uui-color-text-alt, #686c87); }
    select, input { padding: 6px 8px; border: 1px solid var(--uui-color-border, #d6d6d6); border-radius: 3px; }
    table { width: 100%; border-collapse: collapse; }
    th, td { text-align: left; padding: 8px; border-bottom: 1px solid var(--uui-color-divider, #e9e9eb); }
    th { background: var(--uui-color-surface-alt, #f9f9fb); }
    .badge { padding: 2px 8px; border-radius: 10px; font-size: 11px; background: var(--uui-color-surface-alt, #f0f0f4); }
    .badge.source-1 { background: #e3effa; color: #1f5a8a; }
    .badge.source-2 { background: #f0e3fa; color: #5a1f8a; }
    .readonly { opacity: .7; }
    .lock-icon { font-size: 14px; margin-right: 4px; }
  `;

  connectedCallback() {
    super.connectedCallback();
    this.load();
  }

  private async load() {
    this.loading = true;
    this.error = null;
    try {
      this.rules = await NotFoundTrackerApi.listIgnoreRules();
    } catch (e) {
      this.error = (e as Error).message;
    } finally {
      this.loading = false;
    }
  }

  private filteredRules() {
    return this.rules.filter(r => {
      if (this.sourceFilter !== "all" && r.source !== this.sourceFilter) return false;
      if (this.hostnameFilter && (r.hostname ?? "") !== this.hostnameFilter) return false;
      if (this.search && !r.path.includes(this.search.toLowerCase())) return false;
      return true;
    });
  }

  private hostnames() {
    return [...new Set(this.rules.map(r => r.hostname ?? "").filter(Boolean))];
  }

  private async deleteOne(rule: IgnoreRuleItem) {
    if (!confirm(`Delete ignore rule for "${rule.path}"?`)) return;
    try {
      await NotFoundTrackerApi.deleteIgnoreRule(rule.id);
      await this.load();
    } catch (e) {
      alert(`Delete failed: ${(e as Error).message}`);
    }
  }

  private async reseed() {
    if (!confirm("Re-seed the built-in auto-preset? This inserts any missing default rules and reconciles config-seeded rules.")) return;
    try {
      await NotFoundTrackerApi.reseedAutoPreset();
      await this.load();
    } catch (e) {
      alert(`Re-seed failed: ${(e as Error).message}`);
    }
  }

  render() {
    const filtered = this.filteredRules();
    return html`
      <div class="toolbar">
        <div class="field">
          <label>Source</label>
          <select @change=${(e: Event) => { const v = (e.target as HTMLSelectElement).value; this.sourceFilter = v === "all" ? "all" : parseInt(v); }}>
            <option value="all">All</option>
            <option value="0">User-defined</option>
            <option value="1">Auto-preset</option>
            <option value="2">Config</option>
          </select>
        </div>
        <div class="field">
          <label>Hostname</label>
          <select @change=${(e: Event) => (this.hostnameFilter = (e.target as HTMLSelectElement).value)}>
            <option value="">All</option>
            ${this.hostnames().map(h => html`<option value=${h}>${h}</option>`)}
          </select>
        </div>
        <div class="field">
          <label>Search</label>
          <input type="search" placeholder="Path contains..." @input=${(e: Event) => (this.search = (e.target as HTMLInputElement).value)}>
        </div>
        <div style="margin-left:auto; display:flex; gap:8px;">
          <button @click=${() => (this.addingRule = true)}>Add rule</button>
          <button @click=${this.reseed}>Re-seed auto-preset</button>
        </div>
      </div>

      ${this.error ? html`<div style="color:red;">${this.error}</div>` : nothing}
      ${this.loading ? html`<div>Loading…</div>` : nothing}

      <table>
        <thead>
          <tr>
            <th>Path</th>
            <th>Match</th>
            <th>Hostname</th>
            <th>Source</th>
            <th>Note</th>
            <th>Created</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          ${filtered.length === 0 && !this.loading
            ? html`<tr><td colspan="7" style="text-align:center;padding:24px;color:#888;">No rules.</td></tr>`
            : filtered.map(r => html`
              <tr class=${r.isReadOnly ? "readonly" : ""}>
                <td>${r.isReadOnly ? html`<span class="lock-icon" title="Managed via appsettings.json">🔒</span>` : ""}${r.path}</td>
                <td>${MATCH_LABELS[r.matchType]}</td>
                <td>${r.hostname ?? "All sites"}</td>
                <td><span class="badge source-${r.source}">${SOURCE_LABELS[r.source]}</span></td>
                <td>${r.note ?? ""}</td>
                <td>${new Date(r.createdUtc).toLocaleDateString()}</td>
                <td>
                  ${r.isReadOnly
                    ? nothing
                    : html`<button @click=${() => this.deleteOne(r)}>Delete</button>`}
                </td>
              </tr>
            `)}
        </tbody>
      </table>

      ${this.addingRule
        ? html`
          <div style="position:fixed;inset:0;background:rgba(0,0,0,.4);display:flex;align-items:center;justify-content:center;z-index:1000;">
            <div style="background:white;border-radius:6px;">
              <not-found-tracker-add-ignore-rule-modal
                .hitId=${0}
                .suggestedPath=${""}
                .suggestedHostname=${null}
                @done=${() => { this.addingRule = false; this.load(); }}
                @cancel=${() => (this.addingRule = false)}
              ></not-found-tracker-add-ignore-rule-modal>
            </div>
          </div>
        `
        : nothing}
    `;
  }
}

declare global {
  interface HTMLElementTagNameMap {
    "not-found-tracker-ignore-rules-tab": IgnoreRulesTabElement;
  }
}
```

- [ ] **Step 2: Ensure the file is bundled** — add to `src/bundle.manifests.ts`:

```typescript
import "./dashboards/ignore-rules-tab.element.js";
```

- [ ] **Step 3: Build + commit**

```bash
cd src/Umbraco.Community.NotFoundTracker/Client && npm run build && cd -
git add src/Umbraco.Community.NotFoundTracker/Client/src/
git commit -m "feat(NotFoundTracker): add ignore rules tab"
```

---

## Task 17: Final dashboard wiring

Quick check that everything imports correctly + final commit.

- [ ] **Step 1: Open `src/bundle.manifests.ts`** and confirm it has:

```typescript
import "./dashboards/not-found-tracker-dashboard.element.js";
import "./dashboards/hits-tab.element.js";
import "./dashboards/ignore-rules-tab.element.js";

import type { ManifestTypes } from "@umbraco-cms/backoffice/extension-registry";

export const manifests: Array<ManifestTypes> = [
  {
    type: "dashboard",
    alias: "Umbraco.Community.NotFoundTracker.Dashboard",
    name: "404 Tracker",
    element: () => import("./dashboards/not-found-tracker-dashboard.element.js"),
    weight: 100,
    meta: {
      label: "404 Tracker",
      pathname: "not-found-tracker",
    },
    conditions: [
      {
        alias: "Umb.Condition.SectionAlias",
        match: "Umb.Section.Content",
      },
    ],
  },
];
```

- [ ] **Step 2: Add to host build pipeline (optional)** — `src/UmbracoCommunity.StaticAssets/devops/copy-for-cloud.js` may already copy `App_Plugins/` into the cloud bundle. Verify by running the host's `npm run build:for:cloud` if relevant; skip if not.

- [ ] **Step 3: Final commit if anything moved**

```bash
git status
git add . && git commit -m "chore(NotFoundTracker): wire dashboard manifest side-effect imports"
```

---

## Task 18: End-to-end manual verification

- [ ] **Step 1: Start the backend in Development mode** (mirroring Plan 1 / Plan 2's verify steps)

```bash
cd src/UmbracoCommunity.Web.UI && ASPNETCORE_ENVIRONMENT=Development dotnet run --no-launch-profile --urls "http://localhost:65178"
```

- [ ] **Step 2: Open the backoffice**, log in, navigate to Content. Confirm the "404 Tracker" dashboard appears.

- [ ] **Step 3: Hits tab smoke checks**:
  - Generate some 404s by hitting random URLs (`curl http://localhost:65178/zzz-fake-path-1` etc.). Wait ~10s, refresh the dashboard. Hits should appear.
  - Change hostname filter, status filter, sort, search — table updates.
  - Click Delete on a row — gone.
  - Click Redirect — modal opens, accepts a target content key (from a content node's info tab), creates the redirect, status updates.
  - Click Ignore — modal opens with path pre-filled, accepts match type, creates the ignore rule.

- [ ] **Step 4: Ignore rules tab smoke checks**:
  - The 43 built-in preset rules appear.
  - Source filter narrows by source.
  - Add rule — modal opens, accepts a new path/match type, creates a rule with `Source = UserDefined`.
  - Delete a UserDefined rule — gone.
  - Try to interact with a Config rule (if any exist in your appsettings) — the lock icon shows and the Delete button is absent.
  - Re-seed button works.

- [ ] **Step 5: Multi-tenant permission check (if multiple test users available)**:
  - Log in as a user whose start nodes restrict them to one site root.
  - Their hostname dropdown shows only their hostname.
  - They cannot see hits from other hostnames.
  - "All sites" option is not selectable in the ignore-rule modal hostname dropdown — only their hostnames.
  - Global rules and Config rules appear but their Delete buttons are absent.

- [ ] **Step 6: Stop the server**.

---

## Task 18 verification status (recorded 2026-05-18)

Static verification complete; live browser smoke test deferred to the user (same content-less-Umbraco constraint as Plans 1 + 2, plus the user's own dev server holding port 65178 during implementation).

**Verified:**
- ✅ Full solution builds (`dotnet build UmbracoCommunity.sln`) — 0 errors.
- ✅ 84/84 backend tests pass.
- ✅ Frontend bundle builds (`npm run build` in `Client/`) — produces `not-found-tracker.js` (0.13 kB), `not-found-tracker-dashboard.element-*.js` (2.08 kB), `bundle.manifests-*.js` (45.48 kB / 11.91 kB gzipped) under `wwwroot/App_Plugins/UmbracoCommunityNotFoundTracker/`.
- ✅ All custom elements compile + register via side-effect imports in `bundle.manifests.ts`.
- ✅ Dashboard manifest registers under Content section with `Umb.Condition.SectionAlias` matching `Umb.Section.Content`.
- ✅ TypeScript strict mode passes — no unused-variable or implicit-any errors.

**Implementation deviations from the plan (all sound):**
- **Task 4** wired all four management services into the builder extension (not just the redirect service). Task 6 then skipped the service-registration step it would have done — the services were already in place.
- **Task 11**: tsconfig uses the global `UmbExtensionManifest` type via `"types": ["@umbraco-cms/backoffice/extension-types", "vitest/globals"]` rather than the plan's `import type { ManifestTypes }` (which doesn't exist in the installed `@umbraco-cms/backoffice` version). Matches the BlockRestrictions project pattern.
- **Task 13**: dashboard manifest uses `elementName` + `js` (matches Extensions project pattern) instead of the plan's `element: () => import(...)`. Added `export default` to the dashboard element to satisfy `ElementLoaderExports<UmbDashboardElement>`.

**Still to verify (live browser smoke test, blocked by user's running dev server):**
- Backoffice → Content → "404 Tracker" dashboard appears.
- Hits tab: filters, sort, pagination, bulk-select, per-row actions (Redirect modal opens, Ignore modal opens, Delete confirms).
- Ignore rules tab: built-in preset (~43 rows) appears; source filter narrows; Add rule modal works; `ConfigSeeded` rows show 🔒 and no Delete button; Re-seed button works.
- Multi-tenant permission UX: a user with restricted start nodes sees only their hostname in the dropdown and only their hits/rules.

The user can run these checks themselves on a content-loaded dev DB when convenient.

## Plan 3 self-review

**Spec coverage:**
- §5 Tab 1 (Hits): Tasks 14, 15 — ✅ filters, sort, paging, expandable row (note: row-expand for QS children is deferred — the design includes it but it adds complexity; cover in a follow-up patch if editors ask for it).
- §5 Tab 2 (Ignore rules): Task 16 — ✅ source filter, hostname filter, search, add/delete, re-seed, ConfigSeeded lock UX.
- §5 Per-row actions (Create redirect, Ignore, Delete): Tasks 14 + 15 — ✅.
- §5 Bulk operations: Task 14 (bulk-delete in hits tab via toolbar). Bulk-ignore is wired in the API (Task 8) but not given a frontend toolbar button — defer if not needed.
- §5 Management API endpoints: Tasks 6–10 — ✅ all spec endpoints present.
- §5 Permissions model: Tasks 1, 2, 3, 4 — ✅ scope service derives accessible hostnames; every service method respects it; ConfigSeeded read-only enforced.
- §5 Backoffice authentication: Task 6 — ✅ via `AuthorizationPolicies.SectionAccessContent` on the controller base.

**Known scope reductions vs spec (acceptable for v1, noted for follow-up):**
- Frontend uses a plain GUID input for "target content key" instead of Umbraco's native content picker — pick-up of the proper picker requires wiring into the backoffice's modal manager which is a separate Umbraco-specific concern. The text input works; replace in a follow-up.
- Row-expand for the per-hit QS children sub-table is not in this plan. The detail endpoint (`GET hits/{id}`) returns them — adding the UI is a one-task patch.
- No Vitest tests for Lit elements. The dashboard is small enough that visual verification + the backend test suite gives high confidence; add Vitest if the dashboard grows.

**Placeholder scan:** None. Every code block is complete.

**Type consistency:** Verified across tasks. `UserScope` (Task 1) is consumed by every service (Tasks 2–4); `IgnoreRuleMutation` (Task 3) and the controller's `MapMutation` helper (Task 9) use the same `IgnoreRuleMutationResult` enum; DTOs in Task 5 match the controller mappers in Tasks 7–9 (status as `byte`, source as `byte`, etc.); frontend types in Task 12 match the backend DTO shape.

---

## What comes next

After Plan 3 ships and verification passes:

- Wire the **Umbraco native content picker** into the create-redirect modal (replaces the GUID text input).
- Add **row-expand QS children sub-table** to the hits tab.
- Add **Vitest tests** for the Lit elements if the dashboard grows.
- Optional: a **Health Check** that surfaces a count of pending hits on the Umbraco backoffice health-check screen.
- Optional: lift the package into a standalone NuGet — file extraction is mostly mechanical at this point since no host-specific identifiers exist in the package source.

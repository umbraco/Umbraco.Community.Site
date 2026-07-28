# Umbraco.Community.NotFoundTracker — Design

**Status:** Draft for implementation
**Date:** 2026-05-16
**Owner:** Sebastiaan Janssen

## Goal

A high-performance 404 tracking and management system for Umbraco, shipped as a self-contained project (`Umbraco.Community.NotFoundTracker`) that can later be extracted as a NuGet package. Editors get a dashboard in the Content section to see what 404s are happening, sort by popularity or recency, ignore noise, and turn legitimate broken links into proper Umbraco redirects — without recording any 404 on the request hot path synchronously.

## Non-goals

- External-URL redirects. We use Umbraco's built-in `IRedirectUrlService`, which only redirects to content nodes.
- Regex / glob ignore patterns. Exact and path-prefix are sufficient.
- Per-hit timestamp histograms. Counts + first/last seen are enough.
- Granular backoffice permissions beyond Content section access + hostname scoping derived from start nodes.

## High-level architecture

A new project at `src/Umbraco.Community.NotFoundTracker/` mirroring `UmbracoCommunity.BlockRestrictions`:

```
Umbraco.Community.NotFoundTracker/
├── NotFoundTrackerComposer.cs
├── Constants.cs
├── Configuration/NotFoundTrackerOptions.cs
├── Routing/
│   ├── NotFoundTrackingContentFinder.cs   # IContentLastChanceFinder
│   └── INotFoundPageResolver.cs           # host plug-in
├── Recording/
│   ├── NotFoundHitChannel.cs              # bounded Channel<NotFoundHitEvent>
│   ├── NotFoundHitWriterService.cs        # BackgroundService draining + batch upsert
│   └── NotFoundHitEvent.cs
├── Services/
│   ├── INotFoundHitService.cs / .cs
│   ├── INotFoundIgnoreRuleService.cs / .cs
│   ├── INotFoundRedirectService.cs / .cs  # wraps IRedirectUrlService
│   ├── INotFoundUserScopeService.cs / .cs
│   └── NotFoundRetentionService.cs        # BackgroundService for purge
├── Matching/
│   ├── UrlNormalizer.cs
│   └── IgnoreRuleMatcher.cs               # hostname-bucketed trie + hash
├── Infrastructure/
│   ├── NotFoundTrackerDbContext.cs        # EF Core
│   ├── DefaultIgnoreRules.cs              # built-in auto-preset
│   └── AutoPresetSeedingService.cs        # IHostedService — seed + reconcile on boot
├── Migrations/
├── Models/
│   ├── Entities/                          # NotFoundHit, NotFoundHitQueryString, NotFoundIgnoreRule
│   └── Api/                               # request/response DTOs
├── Controllers/NotFoundTrackerApiController.cs
└── Client/                                # Vite + Lit dashboard
    └── src/dashboards/not-found-tracker/
```

The host (`UmbracoCommunity.Web`) collapses its existing `PageNotFoundContentFinder` into a `CommunitySitePageNotFoundResolver : INotFoundPageResolver` and registers it via a single composer call to `builder.AddNotFoundTracker()`.

## Data model

Three tables under the package's EF Core context.

### `NotFoundHits`

| Column | Type | Notes |
|---|---|---|
| `Id` | int PK | |
| `Hostname` | nvarchar(255) | Lowercased. Empty string for "no host" edge case. |
| `Path` | nvarchar(2048) | Lowercased, querystring stripped, leading `/` kept. |
| `HitCount` | bigint | |
| `FirstSeenUtc` | datetime2 | |
| `LastSeenUtc` | datetime2 | Indexed. |
| `LastUserAgent` | nvarchar(512)? | |
| `Status` | tinyint | 0=Active, 1=IgnoredManually, 2=Redirected. |

Unique index `(Hostname, Path)` drives the upsert. Additional indexes on `LastSeenUtc DESC` and `HitCount DESC` for sort + purge.

### `NotFoundHitQueryStrings`

| Column | Type | Notes |
|---|---|---|
| `Id` | int PK | |
| `HitId` | int FK → `NotFoundHits.Id` ON DELETE CASCADE | |
| `QueryString` | nvarchar(2048) | Raw as received. |
| `HitCount` | bigint | |
| `LastSeenUtc` | datetime2 | Indexed for shorter QS-purge. |

Unique index `(HitId, QueryString)`.

### `NotFoundIgnoreRules`

| Column | Type | Notes |
|---|---|---|
| `Id` | int PK | |
| `Hostname` | nvarchar(255)? | Null = global. |
| `MatchType` | tinyint | 0=Exact, 1=PathPrefix. |
| `Path` | nvarchar(2048) | Lowercased on save. |
| `Source` | tinyint | 0=UserDefined, 1=AutoPreset, 2=ConfigSeeded. |
| `Note` | nvarchar(500)? | |
| `CreatedUtc` | datetime2 | |

Index `(Hostname, MatchType, Path)`. Whole table is cached in memory in `IgnoreRuleMatcher`.

### Retention

Driven by `NotFoundTrackerOptions`, applied by `NotFoundRetentionService` running once per hour:

- `NotFoundHits` (Status = Active): purged when `LastSeenUtc < now - 90 days`.
- `NotFoundHits` (Status = Redirected/IgnoredManually): purged when `LastSeenUtc < now - 7 days`.
- `NotFoundHitQueryStrings`: purged when `LastSeenUtc < now - 14 days`. Cascade-deleted whenever the parent row is purged, deleted, ignored, or redirected.

## Recording pipeline

### Hot path

```
HTTP request → routing miss → IRedirectUrlService check (Umbraco built-in) → miss
  ↓
NotFoundTrackingContentFinder.TryFindContent(request):
  hostname = request.Domain?.Name ?? request.Uri.Host  (lowercased)
  path     = request.Uri.AbsolutePath                  (lowercased, normalized)
  qs       = request.Uri.Query                         (raw, may be empty)

  if IgnoreRuleMatcher.IsIgnored(hostname, path):
      skip recording
  else:
      NotFoundHitChannel.Writer.TryWrite(NotFoundHitEvent{...})  // non-blocking; drops if full

  page = INotFoundPageResolver.ResolveAsync(request)
  request.SetPublishedContent(page); SetResponseStatus(404)
```

Zero awaited I/O. Channel cap (default 10,000) bounds memory under hostile traffic. Ignore-rule filtering happens before channel write so a scanner flood on ignored paths can't displace legitimate events.

### Background drain

`NotFoundHitWriterService : BackgroundService`:

```
loop until shutdown:
  await Task.WhenAny(timer.WaitForNextTickAsync(), channel.Reader.WaitToReadAsync())
  events = drain up to WriterBatchSize (default 500)
  if empty, continue

  group by (hostname, path):
    upsert main row (increment HitCount by group count, set LastSeenUtc to max, update LastUserAgent)
    group events by querystring:
      upsert NotFoundHitQueryStrings (HitId, QueryString)
  on shutdown: drain remaining with 5s grace period.
```

One DB round trip per `(hostname, path)` group + one per `(HitId, qs)` group per flush. Idempotent batch transactions — a crash mid-batch loses at most one flush interval of in-memory events.

### Dashboard actions

- **Create redirect**: `IRedirectUrlService.Register(url, contentKey, culture)` → flip `Status = Redirected` → cascade-delete QS children.
- **Add to ignore list**: insert `NotFoundIgnoreRule` → call `IgnoreRuleMatcher.Refresh()` → flip `Status = IgnoredManually` → cascade-delete QS children.
- **Delete row**: hard delete (cascade drops QS rows).

## Ignore rules

### Rule shape

- **MatchType**: Exact (whole path) or PathPrefix.
- **Path**: lowercased, leading `/` required. Trailing `/` ignored on exact match; significant on prefix.
- **Hostname**: optional. Null = global; otherwise scoped to one hostname.
- **Note**: free text.

No regex/glob/querystring patterns. Hit-log key is already lowercased + QS-stripped, so a single rule covers every casing/QS variant.

### `IgnoreRuleMatcher` internals

Singleton holding an immutable snapshot. Rules bucketed by hostname → per-bucket structure:

- `HashSet<string>` of exact paths → O(1).
- Path-segment **trie** of prefix rules → O(URL segment depth, ~5-10) regardless of rule count.

Per-request cost: 2 hash lookups + 2 trie walks (one for the request's hostname bucket, one for the global bucket). Stays single-digit µs even at 10k+ rules. No caps on rule count.

Cache invalidation: every dashboard mutation calls `Refresh()`; on boot, loaded once.

### Auto-preset

Static list in `Infrastructure/DefaultIgnoreRules.cs`. Seeded on boot as `Source = AutoPreset` with **insert-if-missing** semantics — editor deletions persist. Disabled wholesale via `NotFoundTrackerOptions.SeedAutoPreset = false`.

Preset entries (all PathPrefix unless noted):

- WordPress/PHP: `/wp-admin`, `/wp-login`, `/wp-content`, `/wp-includes`, `/wp-json`, `/xmlrpc.php` (Exact), `/wlwmanifest.xml` (Exact).
- Config/secret probes: `/.env` (Exact), `/.git`, `/.svn`, `/.aws`.
- PHP/classic admin: `/phpmyadmin`, `/pma`, `/myadmin`, `/adminer.php` (Exact), `/admin.php` (Exact).
- IIS/.NET legacy: `/owa`, `/ecp`, `/autodiscover`, `/Telerik.Web.UI.WebResource.axd` (Exact), `/elmah.axd` (Exact), `/trace.axd` (Exact).
- CMS scanners: `/drupal`, `/joomla`, `/typo3`, `/magento`, `/bitrix`, `/laravel`, `/.htaccess` (Exact), `/web.config` (Exact).
- Misc nuisance: `/cgi-bin`, `/scripts`, `/cgi`, `/server-status` (Exact), `/server-info` (Exact), `/HNAP1` (Exact), `/boaform`, `/setup.cgi` (Exact), `/.DS_Store` (Exact), `/Thumbs.db` (Exact).
- Noisy-but-debatable: `/ads.txt` (Exact), `/app-ads.txt` (Exact), `/security.txt` (Exact).

Deliberately **excluded** so editors see them: `/sitemap.xml`, `/robots.txt`, `/favicon.ico`, `/apple-touch-icon*`, `/.well-known/`.

### Config-seeded rules

Declared in `appsettings.json` under `NotFoundTracker.AdditionalAutoPresetRules`. Seeded as `Source = ConfigSeeded` with **reconcile-on-boot** semantics:

1. Compute desired config-seeded set.
2. Insert any missing.
3. Delete any existing `ConfigSeeded` rows no longer in config.

Config rules are **read-only from the dashboard** (edit/delete API returns 403). Removing them requires editing `appsettings.json`. Built-in `AutoPreset` rules remain dashboard-deletable.

## Configuration

```csharp
public sealed class NotFoundTrackerOptions
{
    public int ActiveRetentionDays { get; set; } = 90;
    public int ActionedRetentionDays { get; set; } = 7;
    public int QueryStringRetentionDays { get; set; } = 14;
    public TimeSpan RetentionSweepInterval { get; set; } = TimeSpan.FromHours(1);
    public TimeSpan WriterFlushInterval { get; set; } = TimeSpan.FromSeconds(5);
    public int WriterBatchSize { get; set; } = 500;
    public int ChannelCapacity { get; set; } = 10_000;
    public bool SeedAutoPreset { get; set; } = true;
    public List<AutoPresetRuleConfig> AdditionalAutoPresetRules { get; set; } = new();
}

public sealed class AutoPresetRuleConfig
{
    public string Path { get; set; } = "";
    public IgnoreMatchType MatchType { get; set; } = IgnoreMatchType.PathPrefix;
    public string? Hostname { get; set; }
    public string? Note { get; set; }
}
```

Bound from `appsettings.json` section `NotFoundTracker` via standard `IOptions<>` pattern.

## Editor UI

One backoffice dashboard under the Content section (alias `notFoundTracker`), built as a Lit element under `Client/src/dashboards/not-found-tracker/`. Two tabs:

### Tab 1: Hits

- Top bar: hostname dropdown, status filter (Active/Redirected/Ignored/All; default Active), path search, sort (Popularity / Recently seen / First seen; default Recent), page size.
- Columns: Path, Hostname, Hits, First seen, Last seen, Status badge, Actions (Create redirect / Ignore / Delete).
- Bulk selection: bulk ignore (one rule per row, asks for match type once), bulk delete. No bulk redirect.
- Expandable row: last UA, querystring variants sub-table, copy-URL button.

### Tab 2: Ignore rules

- Top bar: source filter (User-defined / Auto-preset / Config / All), hostname filter, search.
- Columns: Path, Match type, Hostname, Source badge, Note, Created, Actions (Edit / Delete — hidden for `ConfigSeeded`).
- Buttons: Add rule, Re-seed auto-preset.

### Permissions

`INotFoundUserScopeService` (per-request) derives accessible hostnames from the user's start nodes via `IDomainService.GetAssignedDomains(...)`. Returns `{ AccessibleHostnames, HasFullAccess }`. `HasFullAccess` true when start nodes include root (`-1`).

Applied to:
- **Hits**: `WHERE Hostname IN (@accessible)`. Single-row endpoints 404 on cross-tenant access (no leak). All mutations re-verify.
- **Hostname dropdown**: `GET hits/hostnames` returns distinct hit-table hostnames ∩ accessible set.
- **Create redirect**: native picker respects start nodes; server verifies (1) hit hostname accessible, (2) target node under accessible start node.
- **Ignore rules**: hostname-scoped rules editable only when the rule's hostname is accessible. Global rules (`Hostname IS NULL`) are visible-but-read-only for non-full-access users; only `HasFullAccess` users mutate them. The "Ignore" modal on a hit row defaults to and locks the hostname for non-full-access users. `ConfigSeeded` rules read-only for everyone.
- **Bulk ops**: filter inaccessible IDs server-side, return `{ processed, skipped }`. No silent failures.

### Management API

Base: `/umbraco/umbracocommunitynotfoundtracker/api/v1`. Secured with `[Authorize(Policy = AuthorizationPolicies.SectionAccessContent)]`.

- `GET hits` — paged, filtered, sorted.
- `GET hits/{id}` — includes QS children.
- `GET hits/hostnames` — accessible distinct hostnames.
- `DELETE hits/{id}` / `POST hits/bulk-delete`.
- `POST hits/{id}/redirect` — `{ targetContentKey, culture? }`.
- `POST hits/{id}/ignore` — `{ matchType, path, hostname?, note? }`.
- `POST hits/bulk-ignore` — `{ ids[], matchType }`.
- `GET ignore-rules` — list (small, no paging).
- `POST/PUT/DELETE ignore-rules[/{id}]`.
- `POST ignore-rules/reseed-auto-preset`.

Every ignore-rule mutation calls `IgnoreRuleMatcher.Refresh()` before returning.

## Host wiring

### Package surface (only public API the host depends on)

```csharp
namespace Umbraco.Community.NotFoundTracker;

public interface INotFoundPageResolver
{
    Task<IPublishedContent?> ResolveAsync(IPublishedRequestBuilder request);
}

public static class NotFoundTrackerBuilderExtensions
{
    public static IUmbracoBuilder AddNotFoundTracker(this IUmbracoBuilder builder);
}
```

`AddNotFoundTracker()` registers the DbContext, options, all internals, both background services, the migration hosted service, the auto-preset seeding hosted service, and calls `SetContentLastChanceFinder<NotFoundTrackingContentFinder>()`. It does **not** register an `INotFoundPageResolver` — the host must.

### Host changes in `UmbracoCommunity.Web`

1. Add project reference to `Umbraco.Community.NotFoundTracker`.
2. Replace `Routing/PageNotFoundContentFinder.cs` with `Routing/CommunitySitePageNotFoundResolver.cs` implementing `INotFoundPageResolver` (same tenant-aware lookup logic, returns the node only).
3. Delete `PageNotFoundContentFinderComposer`.
4. New `NotFoundTrackerHostComposer` that calls `builder.AddNotFoundTracker()` and registers the resolver.
5. Optional `NotFoundTracker` section in `appsettings.json` / `appsettings.Development.json` for tuning.

No view or Razor changes. Package owns the dashboard; host owns only the resolver.

### Future packaging

- `csproj` aligns with Umbraco/.NET versions via the existing Central Package Management.
- `Client/` output → `wwwroot/App_Plugins/Umbraco.Community.NotFoundTracker/`, packed into the NuGet.
- No `UmbracoCommunity.Web` / `community.umbraco.com` strings in the package source — verified by grep.

## Observability

- `ILogger<NotFoundHitWriterService>`: Debug on each drain ("drained N, upserted M, Xms"); Warning when channel rejects writes (throttled, once-per-minute); Error on upsert failure (single retry on next flush).
- `ILogger<NotFoundRetentionService>`: Information on each sweep ("deleted N hits, M QS, K actioned").
- No custom metrics in v1.

## Testing

### Unit (xUnit, `tests/Umbraco.Community.NotFoundTracker.Tests/`)

- `UrlNormalizer`: path normalization, edge cases (long paths, non-ASCII, double slashes, encoding).
- `IgnoreRuleMatcher`: exact + prefix correctness, hostname scoping, case-folding. Property-based test against a brute-force reference impl. Perf assertion: 10k rules, 100k lookups under ~500ms.
- `NotFoundHitChannel` + writer: in-memory SQLite, 10k synthetic events, concurrent producers, channel-full drop behaviour, graceful shutdown drain.
- `AutoPresetSeedingService`: `AutoPreset` insert-if-missing; deletions persist. `ConfigSeeded` reconcile (add/remove). `UserDefined` collision safety.
- `UserScopeService`: mocked `IDomainService` + `IBackOfficeSecurityAccessor` across full-access / single-tenant / multi-start-node / wildcard-domain users.

### Integration (`tests/Umbraco.Community.NotFoundTracker.IntegrationTests/`)

- End-to-end recording: request → finder → channel → flush → DB row. Repeat asserts increment.
- Ignore rule honored: no DB row on ignored path.
- Redirect action: hit row → API call → `IRedirectUrlService` has registered URL, hit row `Status = Redirected`, QS children gone.
- Retention sweep: backdated rows → manual sweep → correct deletions across all three windows.
- Permissions: two test users (full-access / single-tenant) → correct filtering, 403s on cross-tenant, accurate `skipped` counts.

### Frontend (Vitest, in `Client/`)

- Tab rendering, sort/filter controls, modal interactions.
- Permission-driven UI: non-full-access mocked scope hides "All sites" + locks global rules.

No Playwright/E2E in v1.

### Manual verification checklist

- Hit non-existent URL → row appears in dashboard within 10s.
- 100 hits to same URL → one row with `HitCount = 100`.
- Hit `/wp-admin` → no row (preset works).
- Create redirect from a hit → revisit → land on target node.
- Add ignore rule → revisit → 404 served, no new row.
- Two backoffice users (different start nodes, different hostnames): each sees only their site's hits and rules; global rules locked.
- Restart app → preset + config rules reconcile correctly; `AutoPreset` deletions persist; `ConfigSeeded` deletions revert (config is authoritative).

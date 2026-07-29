---
tags: [caching, output-cache, sessionize, performance]
---

# Output cache policies for slow upstream APIs

When a page depends on a third-party API you don't control — Sessionize, in this case — the API's speed and availability become your page's speed and availability, unless you put something in between. This refinement doesn't build on another tutorial in this suite; it's a self-contained look at how the Sessionize integration protects itself with two independent caching layers, not one, and why a single `[OutputCache]` attribute — however tempting — isn't the whole story.

## The problem

Every uncached hit on `/api/sessionize/*` triggers a real network round trip to Sessionize's public API. That's fine occasionally; it's a problem the moment more than a handful of visitors land on the program page around the same cache miss, or Sessionize itself is slow, rate-limiting, or just having a bad day. The failure mode without any protection is exactly what you'd expect: your page's response time is now Sessionize's response time, and if Sessionize 500s, so does your program page.

## Why the obvious fix doesn't work

**Slapping [`OutputCache`](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/output) on the controller and calling it solved.** It genuinely helps, but it only caches at the ASP.NET Core pipeline boundary — the HTTP response, not the work that produces it. The moment that cache entry expires or a new instance spins up with a cold cache, the request falls straight through to the controller action, which calls Sessionize directly. If Sessionize errors on exactly that request, `[OutputCache]` alone has nothing left to fall back to — it only ever cached success.

**Reaching for `ResponseCaching` instead.** This site already registers ASP.NET Core's `ResponseCaching` middleware site-wide (excluding `/media`), so it's right there. It just doesn't do anything for either of this repo's cached endpoints: `ResponseCaching` is opt-in via response headers the *app* has to set (`Cache-Control`, etc.), and neither the Blog nor Sessionize controllers set any. It's registered, it's inert, and reaching for it instead of `[OutputCache]`'s named policies would mean adding header-setting code that duplicates what a policy already gives you declaratively.

**One cache duration to rule them all.** If you're going to cache the HTTP response *and* the data behind it, it's tempting to wire both to the same config value — one knob, one number, simpler to reason about. This codebase deliberately uses two different ones, because the two layers protect against two different things: the HTTP-level cache exists so a redeploy or scale-out doesn't leave every instance hammering Sessionize at once; the data-level cache exists so Sessionize itself only gets called occasionally, full stop. Collapsing them into one number means picking a bad compromise for at least one of those jobs.

## Our approach

Two independently-configured caching layers, stacked on top of each other:

1. **`[OutputCache(PolicyName = OutputCachePolicies.ExternalApi)]`** on every `SessionizeApiController` action — a short, purely time-based ASP.NET Core output cache at the HTTP boundary.
2. **`SessionizeApiClient.GetAllDataAsync()`'s own `IMemoryCache`**, one level further in, backed by a disk copy that gets read back and served — with no age limit — the moment a live call to Sessionize fails for any reason.

Neither layer knows the other exists. That's a deliberate simplicity trade-off, covered in Trade-offs below.

## Walkthrough

### Step 1 — Two named policies, one registration point

[`Extensions/UmbracoBuilderExtensions.cs`](../../../src/UmbracoCommunity.Web/Extensions/UmbracoBuilderExtensions.cs):

```csharp
public static class OutputCachePolicies
{
    public const string ContentDriven = "ContentDriven";
    public const string ExternalApi = "ExternalApi";
}

public static class OutputCacheTags
{
    public const string BlogContent = "blog-content";
}

public static IUmbracoBuilder AddOutputCaching(this IUmbracoBuilder builder)
{
    var cacheOptions = new OutputCacheOptions();
    builder.Config.GetSection(OutputCacheOptions.SectionName).Bind(cacheOptions);

    builder.Services.AddOutputCache(options =>
    {
        options.AddPolicy(OutputCachePolicies.ContentDriven, policy =>
            policy.Expire(TimeSpan.FromSeconds(cacheOptions.ContentDrivenDurationSeconds))
                  .Tag(OutputCacheTags.BlogContent));

        options.AddPolicy(OutputCachePolicies.ExternalApi, policy =>
            policy.Expire(TimeSpan.FromSeconds(cacheOptions.ExternalApiDurationSeconds)));
    });

    return builder;
}
```

Only two policies exist in this codebase — there's no third. `OutputCacheOptions` is bound with a plain `GetSection(...).Bind(...)` call inside this method itself, not registered via `builder.Services.Configure<T>()` — so unlike most options classes elsewhere in this repo, it's never injectable as `IOptions<OutputCacheOptions>`; it only ever exists as this one local variable, read once at startup to build the policies.

### Step 2 — Applying a policy is one attribute, and the default cache key is usually enough

Every action in [`SessionizeApiController.cs`](../../../src/UmbracoCommunity.Web/Features/Sessionize/Controllers/SessionizeApiController.cs) — `sessions`, `sessions/{sessionId}`, `speakers`, `speakers/{speakerId}`, `schedule`, `categories` — carries the identical attribute:

```csharp
[HttpGet("sessions/{sessionId}")]
[OutputCache(PolicyName = OutputCachePolicies.ExternalApi)]
public async Task<IActionResult> GetSession(string sessionId)
```

Nothing in this repo calls `.SetVaryByQuery(...)` or any other vary-by configuration — a repo-wide search for it comes up empty. That's not an oversight: the output cache's default key already includes the full request path, and `sessionId`/`speakerId` are *route* segments, not query parameters, so two different session IDs are two different cache keys automatically. [`BlogApiController.GetPosts`](../../../src/UmbracoCommunity.Web/Controllers/Api/BlogApiController.cs), which does take real query parameters (`page`, `pageSize`, `tag`, `category`) under `[OutputCache(PolicyName = OutputCachePolicies.ContentDriven)]`, relies on the same default behaviour — the output cache's key includes the query string as a whole, so each distinct combination of filters gets its own cache entry with no extra configuration.

### Step 3 — One policy gets evicted on a real event; the other only ages out

[`Notifications/BlogContentCacheInvalidationHandler.cs`](../../../src/UmbracoCommunity.Web/Notifications/BlogContentCacheInvalidationHandler.cs) listens for Umbraco content-cache-refresh notifications, checks whether the changed content type is `Article` or `Blog`, and calls `_outputCacheStore.EvictByTagAsync(OutputCacheTags.BlogContent, ...)` — the tag `ContentDriven` responses are stamped with in Step 1. Publish a blog post, and every cached blog API response tagged with it disappears immediately, regardless of how much of its TTL was left.

`ExternalApi` responses have no tag and no equivalent handler, because there's no equivalent event: nothing in this system knows when Sessionize's own data changes. The only way an `ExternalApi`-cached response goes away is its TTL expiring — which is exactly what "time-based expiration for data we don't control" (the policy's own doc comment) means in practice.

### Step 4 — A second cache, one layer further in, that `[OutputCache]` never sees

[`SessionizeApiClient.GetAllDataAsync()`](../../../src/UmbracoCommunity.Web/Features/Sessionize/Infrastructure/SessionizeApiClient.cs) is the one method that actually talks to Sessionize's network API — every other public method on the client (`GetSessionsAsync`, `GetSpeakersAsync`, `GetScheduleAsync`, …) calls through it and shapes the result in memory. It has its own cache, entirely independent of anything ASP.NET Core knows about:

```csharp
var cacheKey = $"sessionize_all_{_options.EventId}";

if (_cache.TryGetValue(cacheKey, out SessionizeAllData? cachedData) && cachedData != null)
{
    return cachedData;
}
// ...fetch from Sessionize...
_cache.Set(cacheKey, allData, TimeSpan.FromMinutes(_options.CacheDurationInMinutes));
```

This is the layer that actually protects Sessionize's API from being hit every time the *outer* `[OutputCache]` entry expires — a cache miss at the HTTP layer doesn't necessarily mean a real network call, because this inner cache is very likely still warm. `_options.EventId` in the cache key is the one Sessionize event this whole deployment is configured for — `SessionizeOptions` binds a single flat `Sessionize` config section with one `EventId`, set once at DI-registration time, not resolved per request. There's no tenant-varying input for this cache key to account for, because there's no tenant-varying input to Sessionize at all in this codebase. (If you do need a cache key that varies per tenant, [`PageNotFoundSuggestionService`](../../../src/UmbracoCommunity.Web/Services/PageNotFoundSuggestionService.cs)'s `$"PageNotFound:{tenantRootId}:{culture}:{max}:{query}"` — built from `currentPage.Root().Id`, per the [multi-tenancy primer](../../primers/multi-tenancy.md) — is the real pattern to reach for.)

### Step 5 — Graceful degradation: stale beats none

The `catch` block around that same network call is where the resilience actually lives:

```csharp
catch (Exception ex)
{
    _logger.LogError(ex, "Error fetching data from Sessionize for event {EventId}", _options.EventId);

    var stale = await TryReadCacheFileAsync(cancellationToken);
    if (stale != null)
    {
        _logger.LogWarning("Returning stale Sessionize data from disk cache for event {EventId}", _options.EventId);
        _cache.Set(cacheKey, stale, TimeSpan.FromMinutes(_options.CacheDurationInMinutes));
        return stale;
    }

    throw;
}
```

Any exception — a timeout, a non-2xx status, malformed JSON — falls back to a JSON copy the same method wrote to disk (`WriteCacheFileAsync`) the last time a real fetch succeeded. If that disk copy exists and parses, it's re-primed into the same `IMemoryCache` key and returned as if it were fresh — **there's no age check on it at all**. Only if there's no disk copy, or it also fails to read, does the exception propagate, which is what turns into the controller's own `HttpRequestException` → 503 / `JsonException` → 502 / generic → 500 mapping.

The disk write itself goes through [`AtomicFile.WriteAllTextAsync`](../../../src/UmbracoCommunity.Web/Utilities/AtomicFile.cs) rather than a plain `File.WriteAllTextAsync`, and the class's own doc comment explains exactly why: on Umbraco Cloud (Azure App Service), a deployment briefly runs the old and new instances side by side, and both writing the same cache file at once used to surface as `IOException`/`UnauthorizedAccessException` in the logs. Writing to a unique temp file and moving it into place — retrying the move through a transient lock — makes the write atomic for readers and safe against exactly that overlap.

### Step 6 — Two knobs, two numbers, deliberately not the same

`OutputCacheOptions.ExternalApiDurationSeconds` defaults to `300` (5 minutes) and is what governs Step 1's HTTP-level cache in production, since nothing overrides it there. `appsettings.Development.json` shortens it to `30` seconds — purely a local-development convenience, so a code change to the controller shows up in a few seconds rather than a few minutes. `SessionizeOptions.CacheDurationInMinutes`, governing Step 4's client-level cache, is `60` minutes in every environment, dev included — there's no reason to shorten it locally, since it's not your own code you're waiting to see change, it's Sessionize's data.

These two settings are never coordinated. [`SessionizeExtensionsApiController`](../../../src/UmbracoCommunity.Extensions/Features/Sessionize/Controllers/SessionizeExtensionsApiController.cs)'s manual `POST /sessionize/refresh-cache` endpoint clears only `SessionizeApiClient`'s `IMemoryCache` entry — it doesn't touch the HTTP-level `IOutputCacheStore` at all, so a manual refresh can still be served back through a stale `ExternalApi`-cached HTTP response until that response's own TTL runs out separately.

## Alternatives we considered

- **`ResponseCaching` instead of named `[OutputCache]` policies.** Already registered site-wide in this codebase, and genuinely simpler in concept — but header-driven, not attribute-driven, so using it here would mean writing the `Cache-Control` header logic a policy already expresses declaratively. It's also not the layer that protects an upstream from being called too often; it protects intermediate caches (browsers, proxies) from re-requesting a response, which is a different problem from the one this refinement solves.
- **One shared cache-duration config value instead of two.** Simpler to configure, worse in practice: the HTTP-level cache and the client-level cache are answering different questions ("how long can a stale HTTP response live" vs. "how long can we go without actually asking Sessionize"), and a single number would force one of those two answers to be wrong.
- **`SetVaryByQuery` on every cached action, defensively.** Not needed here — every cache-key-relevant input in this codebase's two `[OutputCache]`-decorated controllers is already either a route segment (Sessionize) or already covered by the output cache's default full-path-plus-query key (Blog's filters). Adding explicit vary-by configuration would be documentation of behaviour the default already provides, not a change in behaviour.

## Trade-offs and known limits

- **The two layers aren't coordinated.** Clearing one doesn't clear the other; a manual cache refresh can still appear to have "not worked" for up to the HTTP layer's own TTL.
- **No age limit on the stale-disk fallback.** If Sessionize were unreachable for a long stretch, this mechanism would keep serving the same disk snapshot indefinitely rather than eventually surfacing an error — "stale beats none" has no upper bound built in.
- **Both layers are per-instance by default.** `[OutputCache]`'s default store is an in-process `MemoryCache` unless a distributed store (Redis, say) is configured — this codebase doesn't configure one — and `SessionizeApiClient`'s own cache is a plain `IMemoryCache` too. On a horizontally-scaled deployment, each instance keeps its own independent copy of both layers, so the actual number of calls Sessionize sees scales with instance count, not just with traffic.
- **No automated tests** cover `SessionizeApiClient`'s fallback path, `AtomicFile`'s concurrent-write handling, or either `[OutputCache]` policy.

## Where to go next

- **[Caching primer](../../primers/caching.md)** — where this pair of policies sits among every other cache in the codebase, and the general "which cache for which job" guidance.
- **[Multi-tenancy primer](../../primers/multi-tenancy.md)** — the `Root()`-based tenant scoping behind `PageNotFoundSuggestionService`'s cache key, referenced above.
- **[Backend primer](../../primers/backend.md#output-caching)** — where output caching sits in the wider request pipeline.

Hopefully that's enough to protect the next slow third-party integration this codebase grows — welcome aboard!

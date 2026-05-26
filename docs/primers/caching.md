---
tags: [primer, caching, performance, runtime-cache, output-cache]
---

# Caching primer

> **Status:** Planned — this page is a stub. The full primer hasn't been written yet; see the [primer backlog](./IDEAS.md) for the framing and motivation.

Caching is everywhere in this codebase but it's scattered. `AppCaches.RuntimeCache` (SVG TagHelper), `MemoryCache` (Vite manifest), `OutputCachePolicies` (API endpoints with named policies), `RequestCache` (per-request memoisation), `IsolatedCaches` (named, longer-lived, evictable as a group). This primer will map which cache to reach for in which situation, and what invalidates each one — so the next time you're about to add a `MemoryCache.Set(...)` somewhere, you've got a fighting chance of picking the right layer.

## What this will cover

- The `AppCaches` family: `RuntimeCache`, `RequestCache`, `IsolatedCaches` — when to use each.
- `MemoryCache` and when to reach for it instead of the Umbraco abstractions.
- The named-policy `OutputCachePolicies` pattern for API endpoints.
- Invalidation: cache tags, Umbraco notifications, time-based expiration.

*If you're picking this up to write, a primer is the orientation layer — keep it short and signpost the tutorials for depth.*

---
tags: [caching, output-cache, sessionize, performance]
---

# Output cache policies for slow upstream APIs

> **Status:** Planned — this page is a stub. The full tutorial hasn't been written yet; see the [tutorial backlog](../IDEAS.md) for the framing and motivation.

When your site's performance is held hostage by a slow or rate-limited third-party API (Sessionize, GitHub, anything off-prem), aggressive caching is usually the answer — and ASP.NET Core's `[OutputCache]` with named policies is the right tool for it. This refinement will walk through the `OutputCachePolicies` class in this repo, when `[OutputCache]` beats `ResponseCaching` (it survives across instances, you control the key, you can vary by query), what cache-key shapes make sense for tenant-scoped data, and how to fail gracefully when the upstream is rate-limited or 500s.

## What this will cover

- Named output-cache policies and how they're registered.
- Cache-key shapes for tenant-scoped data.
- When `[OutputCache]` is the right tool vs. `ResponseCaching` vs. `IMemoryCache`.
- Graceful degradation when the upstream API is unavailable.

*If you're picking this up to write, follow the section structure in [Contributing a new tutorial](../README.md#contributing-a-new-tutorial).*

---
tags: [search, examine, multi-tenant, lucene]
---

# Site search backed by Umbraco's Examine ExternalIndex

> **Status:** Planned — this page is a stub. The full tutorial hasn't been written yet; see the [tutorial backlog](../IDEAS.md) for the framing and motivation.

"How do I add search to my Umbraco site?" is a perennial community question, and most answers stop at the single-tenant happy path. This tutorial will walk through the recently-added search on the Umbraco Community site: a `SearchPage` doc type, a typed `SearchService` that queries Umbraco's Examine `ExternalIndex`, a render controller that paginates the results, and the multi-tenant twist — only return hits under the current tenant's content root, so a search on Site A doesn't leak Site B results.

## What this will cover

- Querying `ExternalIndex` from a typed service.
- Tenant-scoping the results via the current page's root node.
- Stripping HTML out of excerpts before display.
- Pagination and the small set of result-ranking trade-offs.

*If you're picking this up to write, follow the section structure in [Contributing a new tutorial](../README.md#contributing-a-new-tutorial).*

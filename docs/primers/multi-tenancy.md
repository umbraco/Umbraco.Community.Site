---
tags: [primer, multi-tenancy, architecture, content-resolution]
---

# Multi-tenancy primer

> **Status:** Planned — this page is a stub. The full primer hasn't been written yet; see the [primer backlog](./IDEAS.md) for the framing and motivation.

One Umbraco instance, several distinct sites, each with its own root content node and its own domain. This primer will tie together the [multi-tenant content resolution tutorial](../tutorials/foundations/multi-tenant-content-resolution.md) (the foundation) and the two refinements layered on it ([per-tenant 404](../tutorials/refinements/per-tenant-404-content-finder.md), [tenant-aware SEO fallback](../tutorials/refinements/tenant-fallback-for-schema-and-seo.md)) into a single five-minute orientation. Read it if you're new to the codebase and need to understand the single invariant that affects nearly everything you'll write here.

## What this will cover

- What the multi-root content tree looks like in the backoffice.
- The `GetSiteSettings()` convention and the small set of helpers that lean on it.
- Domain-binding for the routing-level lookups that don't have a `currentPage` to start from.
- The intentional cross-tenant cases (RSS, the 404 fallback) and why they're not a smell.

*If you're picking this up to write, a primer is the orientation layer — keep it short and signpost the tutorials for depth.*

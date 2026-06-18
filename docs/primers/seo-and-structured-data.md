---
tags: [primer, seo, schema-org, meta-tags]
---

# SEO and structured data primer

> **Status:** Planned — this page is a stub. The full primer hasn't been written yet; see the [primer backlog](./IDEAS.md) for the framing and motivation.

Schema.NET, the schema builders (`ArticleSchemaBuilder`, `OrganizationSchemaBuilder`, `BreadcrumbSchemaBuilder`), the `MetaTags` ViewComponent, OpenGraph and Twitter Cards in `Layout.cshtml`, sitemap generation, and canonical URL handling. This primer will be the orientation layer that holds the whole SEO surface in one place; the [tenant-aware schema fallback tutorial](../tutorials/refinements/tenant-fallback-for-schema-and-seo.md) is the deep dive for one specific corner of it.

## What this will cover

- Schema.NET and the schema-builder pipeline.
- The `MetaTags` ViewComponent and the layout slot it fills.
- OpenGraph, Twitter Cards, and canonical URLs.
- Sitemap generation and the per-tenant scoping that drives it.

*If you're picking this up to write, a primer is the orientation layer — keep it short and signpost the tutorials for depth.*

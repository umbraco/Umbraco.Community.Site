---
tags: [vite, dual-build, backoffice, app-plugins]
---

# One Vite project, two bundles: public site + backoffice extensions

> **Status:** Planned — this page is a stub. The full tutorial hasn't been written yet; see the [tutorial backlog](../IDEAS.md) for the framing and motivation.
> **Prerequisites (when written):** This refinement will build on the planned [Wiring Vite's manifest into Umbraco's Razor pipeline](../foundations/vite-umbraco-manifest-integration.md) foundation.

The Vite project in `UmbracoCommunity.StaticAssets` builds two completely different things from the same toolchain: the public-site bundle that the Razor views consume, and a backoffice bundle that lands in `App_Plugins/UmbracoCommunityGitHubUsers/`. The trick is a `BUILD_TARGET=backoffice` environment switch that swaps entry points and output paths without needing to fork the config. This refinement will walk through the dual-build pattern — one toolchain serving two very different consumers — and the trade-offs against the alternative of running two separate Vite projects (which is what the Extensions and BlockRestrictions clients in this repo do).

## What this will cover

- The `BUILD_TARGET` switch in `vite.config.ts`.
- Separate entry points and output paths per target.
- When to share a build vs. when to spin up a second Vite project entirely.
- Caveats: shared `node_modules`, different module-loading conventions in backoffice vs. public site.

*If you're picking this up to write, follow the section structure in [Contributing a new tutorial](../README.md#contributing-a-new-tutorial).*

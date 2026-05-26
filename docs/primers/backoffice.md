---
tags: [primer, backoffice, extensions, app-plugins, vite]
---

# Backoffice extensions primer

> **Status:** Planned — this page is a stub. The full primer hasn't been written yet; see the [primer backlog](./IDEAS.md) for the framing and motivation.

The [frontend primer](./frontend.md) covers the public site but deliberately punts on `UmbracoCommunity.Extensions/Client/` and `UmbracoCommunity.BlockRestrictions/Client/` — the two backoffice client codebases this repo ships. This primer will thread them together: the App_Plugins manifest format, the Umbraco backoffice design system (`@umbraco-cms/backoffice`), how property editors / workspace views / dashboards are registered, and the dual-Vite-project setup that keeps backoffice and public-site builds isolated from one another.

## What this will cover

- The App_Plugins manifest format and what Umbraco does with it.
- The Umbraco backoffice design system (`@umbraco-cms/backoffice`).
- Registering property editors, workspace views, and dashboards.
- Why backoffice extensions get their own Vite project per package.

*If you're picking this up to write, a primer is the orientation layer — keep it short and signpost the tutorials for depth.*

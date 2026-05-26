---
tags: [vite, manifest, razor, tag-helper, hmr]
---

# Wiring Vite's manifest into Umbraco's Razor pipeline

> **Status:** Planned — this page is a stub. The full tutorial hasn't been written yet; see the [tutorial backlog](../IDEAS.md) for the framing and motivation.

Modern frontend tooling talking to a server-rendered Razor view is a deceptively fiddly thing to get right — most "I want Vite (or React, or Lit) in my .NET project" tutorials online either skip the dev/prod handover or get it wrong. This tutorial will walk through how the Umbraco Community site does it: a pair of TagHelpers that point at the Vite dev server (`:5123`) for HMR in development and at the hashed assets named in `manifest.json` in production, with cache invalidation that survives a deploy without an app restart.

## What this will cover

- The two-mode TagHelper pair (`<script vite-src>` / `<link vite-href>`) and how the C# environment switches their behaviour.
- Reading and caching `manifest.json` with `IFileProvider.Watch`.
- Where the `vite-client="true"` HMR bootstrap belongs (and why it appears exactly once per page).
- The "I forgot to start `npm run dev`" failure mode and how to recognise it fast.

*If you're picking this up to write, follow the section structure in [Contributing a new tutorial](../README.md#contributing-a-new-tutorial).*

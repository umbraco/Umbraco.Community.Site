# Primers

Concept-oriented overviews of how the major areas of this codebase hang together.

A primer doesn't tell you how to add a new thing (that's what the `BUILDING_*` how-to docs are for) and doesn't explain why one specific bit of code looks the way it does (that's tutorials). It gives you the **lay of the land** for an area, with links out to the deeper docs for each topic.

If you're new to the codebase, start here. If you've been here a while and someone asks "how does X work in our project", point them at the relevant primer.

## What's here

- **[Frontend primer](frontend.md)** — the Vite-powered public-site frontend in `UmbracoCommunity.StaticAssets`. Covers the dual dev workflow, the manifest-driven Razor integration, the entrypoint convention, Lit + PostCSS, testing, and what builds for production. Backoffice frontends are signposted at the end.

## Adding a new primer

A new primer earns its keep when an area of the codebase has:

- More than one or two surface-level concepts a new contributor needs to hold in their head, **and**
- A scattering of how-to / tutorial / reference docs that benefit from being threaded together.

If the answer fits in a paragraph in CLAUDE.md, it doesn't need a primer. If you find yourself writing the same "let me explain how X works in this project" thread three times, it does.

See [`IDEAS.md`](./IDEAS.md) for candidate primers worth writing.

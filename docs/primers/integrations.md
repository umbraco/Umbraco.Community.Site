---
tags: [primer, integrations, sessionize, github, third-party]
---

# Third-party integrations primer

> **Status:** Planned — this page is a stub. The full primer hasn't been written yet; see the [primer backlog](./IDEAS.md) for the framing and motivation.

Sessionize (events), GitHub (release tracking), Google Maps (community map), Matomo (analytics), Intercom (chat), Cookiebot (consent). Each integration brings its own configuration shape, its own API client pattern, its own dev-mode story — and they all live in different parts of the codebase. This primer will inventory what's wired up, where each integration lives, and what to think about when adding a new one.

## What this will cover

- The standard integration shape in this codebase: options + client + composer.
- Where each current integration lives, and what it touches.
- Dev-mode patterns: don't hammer prod APIs from your laptop.
- When an integration earns its own `Features/<Name>/` folder rather than living flat.

*If you're picking this up to write, a primer is the orientation layer — keep it short and signpost the tutorials for depth.*

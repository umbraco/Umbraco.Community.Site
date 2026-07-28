---
tags: [csp, security, razor, nonce, tag-helper]
---

# A nonce-based Content Security Policy in ASP.NET Core Razor

> **Status:** Planned — this page is a stub. The full tutorial hasn't been written yet; see the [tutorial backlog](../IDEAS.md) for the framing and motivation.

A strict Content Security Policy that bans inline scripts will catch most XSS vectors at the browser level — and it will also make life impossible for every legitimate inline script your views happen to emit. The standard answer is to nonce every inline script and stamp the same nonce into the CSP header, so the browser allows scripts whose nonce matches and blocks everything else. This tutorial will walk through the `NonceTagHelper` + Joonasw integration this site uses, plus a per-request escape hatch for the rare endpoint that needs CSP disabled entirely. CSP-in-.NET is poorly documented; the aim is for this to be the post one of us wishes had existed.

## What this will cover

- Generating a per-request nonce and exposing it to Razor.
- The `NonceTagHelper` and the `asp-add-nonce` attribute pattern.
- Stamping the nonce into the CSP header on the way out.
- The `DisableCspMiddleware` escape hatch and when to reach for it.

*If you're picking this up to write, follow the section structure in [Contributing a new tutorial](../README.md#contributing-a-new-tutorial).*

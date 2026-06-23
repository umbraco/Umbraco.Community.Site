---
tags: [animation, intersection-observer, performance, accessibility]
---

# Polite animation with IntersectionObserver, requestAnimationFrame, and reduced-motion

> **Status:** Planned — this page is a stub. The full tutorial hasn't been written yet; see the [tutorial backlog](../IDEAS.md) for the framing and motivation.

The `<dc-image-slider>` component auto-scrolls a row of images — but you don't want it to keep burning a CPU core when the user can't see it, or when they've asked the OS to dial down motion. This tutorial will walk through the small composition that makes the animation cheap *and* polite: `requestAnimationFrame` for the loop itself, `IntersectionObserver` to pause when the slider scrolls off-screen, `visibilitychange` for tab switches, and `prefers-reduced-motion` for accessibility. The general lesson is how to animate something well without reaching for a library.

## What this will cover

- Why `requestAnimationFrame` is the right loop primitive for animation.
- Wiring `IntersectionObserver` to start and pause the loop.
- Handling tab visibility with the `visibilitychange` event.
- Respecting `prefers-reduced-motion: reduce` and falling back gracefully.

*If you're picking this up to write, follow the section structure in [Contributing a new tutorial](../README.md#contributing-a-new-tutorial).*

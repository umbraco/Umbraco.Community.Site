---
tags: [web-components, slider, touch, lit]
---

# Building a touch-friendly drag-to-scroll slider in vanilla web components

> **Status:** Planned — this page is a stub. The full tutorial hasn't been written yet; see the [tutorial backlog](../IDEAS.md) for the framing and motivation.

Carousel libraries like Swiper and Embla are great until you measure the bundle cost. This tutorial will walk through the `<dc-slider>` component in this repo — a small Lit web component that supports touch drag (follows the finger and snaps to the nearest slide on release), desktop hover-zone navigation, and explicit arrow buttons as an opt-in. The aim is to show what a usable scroller looks like when it's built from scratch with native APIs rather than pulled in as a dependency.

## What this will cover

- Touch drag that follows the finger and snaps on release.
- Desktop hover-zone navigation and the explicit-arrow-button opt-in.
- Keyboard and screen-reader accessibility for a custom scroller.
- How to keep the component small enough that "no library" is genuinely cheaper.

*If you're picking this up to write, follow the section structure in [Contributing a new tutorial](../README.md#contributing-a-new-tutorial).*

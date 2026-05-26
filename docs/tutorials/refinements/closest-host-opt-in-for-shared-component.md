---
tags: [web-components, composition, slider, shared-component]
---

# One web component, two host contexts: opt-in behaviour via `closest()`

> **Status:** Planned — this page is a stub. The full tutorial hasn't been written yet; see the [tutorial backlog](../IDEAS.md) for the framing and motivation.
> **Prerequisites (when written):** This refinement will build on the planned [drag-to-scroll slider](../foundations/drag-to-scroll-with-snap.md) foundation.

The `<dc-slider>` web component is reused by both the slider block and the blog showcase block on this site. Each host wants slightly different behaviour — the blog showcase needs explicit arrow buttons; the slider block uses hover zones. Rather than duplicating the component or adding a `host-type` parameter that compounds with every new host, `<dc-slider>` uses `closest('.dc-slider-block, .dc-blog-showcase-block')` to find its ancestor and respects a `has-buttons` opt-in class on that ancestor. This refinement will show how to let one web component serve multiple host contexts without parameter explosion.

## What this will cover

- Why `host-type="..."` parameters compound badly as host count grows.
- Using `closest()` to find the rendering ancestor.
- Opt-in classes on the ancestor as feature flags for the inner component.
- When this pattern stops scaling (and what to reach for when it does).

*If you're picking this up to write, follow the section structure in [Contributing a new tutorial](../README.md#contributing-a-new-tutorial).*

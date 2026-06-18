---
tags: [css, postcss, design-tokens, utility-classes]
---

# Generating utility classes from design tokens with a custom PostCSS mixin

> **Status:** Planned — this page is a stub. The full tutorial hasn't been written yet; see the [tutorial backlog](../IDEAS.md) for the framing and motivation.

Utility-first CSS frameworks like Tailwind give you the ergonomics of `.pt-md` and `.mx-xs` — but they want to own your design system. The rhythm mixin in this repo flips that arrangement: you write your spacing tokens in `root.css` as CSS custom properties, hand them to a small `postcss-mixins` rule, and you get the same utility classes generated for you — driven entirely by your own tokens, with no opinion from a framework. This tutorial will walk through writing the mixin, the modifier suffixes it emits, and how to layer it on an existing PostCSS pipeline.

## What this will cover

- Authoring the mixin in TypeScript.
- The modifier convention (`-xxs`, `-xs`, `-sm`, *(no suffix)*, `-md`, `-lg`, `-xl`, `-0`).
- Wiring the mixin into the Vite PostCSS pipeline.
- When generated utility classes earn their keep over hand-written component CSS.

*If you're picking this up to write, follow the section structure in [Contributing a new tutorial](../README.md#contributing-a-new-tutorial).*

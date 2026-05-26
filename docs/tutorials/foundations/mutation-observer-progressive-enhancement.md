---
tags: [progressive-enhancement, mutation-observer, forms, web-components]
---

# Progressive enhancement of async-rendered DOM with MutationObserver

> **Status:** Planned — this page is a stub. The full tutorial hasn't been written yet; see the [tutorial backlog](../IDEAS.md) for the framing and motivation.

Sometimes the DOM you want to enhance hasn't been rendered yet. The `<dc-form-steps>` component in this repo enhances Umbraco Forms — which render asynchronously inside `<umb-forms-render>` — into a multi-step form, but it can't do that until the field groups exist. This tutorial will walk through the small `MutationObserver`-based pattern that waits for the target DOM to appear, runs the enhancement once, and disconnects cleanly. The pattern transfers anywhere a third-party widget owns the timing of its own render.

## What this will cover

- Why a `MutationObserver` beats polling for "wait for this DOM to appear".
- Disconnecting cleanly once the enhancement has run.
- Cleaning up when the host element itself is removed.
- The Umbraco Forms specifics: hooking `<umb-forms-render>` and grouping `.umbraco-forms-fieldset` elements into steps.

*If you're picking this up to write, follow the section structure in [Contributing a new tutorial](../README.md#contributing-a-new-tutorial).*

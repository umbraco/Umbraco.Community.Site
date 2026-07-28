---
tags: [primer, content-modelling, document-types, models-builder]
---

# Content modelling primer

> **Status:** Planned — this page is a stub. The full primer hasn't been written yet; see the [primer backlog](./IDEAS.md) for the framing and motivation.

Document types, element types, block types, and the compositions (`ICompositionPageConfiguration`, `ICompositionSeo`, ...) that share fields across them. The auto-generated `PublishedModels` namespace that turns all of those into typed C# classes. The view-model-builder pipeline that converts `IPublishedContent` into the view-shaped models the Razor templates actually render against. This primer will thread the `Models/` folder structure together with the `BUILDING_PAGES.md` and `BUILDING_BLOCKS.md` how-to guides so a new contributor can hold the whole shape in their head.

## What this will cover

- Document types, element types, and block types — what each is for.
- Compositions for sharing fields across content types without inheritance gymnastics.
- The auto-generated `PublishedModels` namespace and Models Builder.
- The view-model-builder pipeline that turns content models into view-shaped models.

*If you're picking this up to write, a primer is the orientation layer — keep it short and signpost the how-tos and tutorials for depth.*

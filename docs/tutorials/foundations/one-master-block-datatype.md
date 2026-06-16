---
tags: [content-modelling, block-editor, data-types, architecture]
---

# One master block data type, restricted per consumer

This is the content-modelling decision that the whole Block Restrictions package exists to serve, written down on its own because it's the *why* behind two other tutorials and it's easy to lose under their mechanics. The short version: keep a single Block Grid (or Block List) data type that holds every block the site could ever use, point every document type at that one data type, and then narrow the offered set *per consumer* — per document type, and by inheritance the content nodes beneath it — with a restriction rule, rather than building a separate data type for every document type that wants a different subset of blocks.

## Why you might want this

A Block Grid / Block List data type carries a fixed list of allowed blocks. Whatever blocks you configure on it, every property — on every document type — that uses that data type offers exactly that list. There's no built-in "this document type only gets a subset" knob.

But "a subset per document type" is almost always what you actually want. A **Blog Article** should offer rich text, an image, a code block, a callout. A **Landing Page** should offer the lot — heroes, sliders, CTA bands, feature grids. A **Press Release**, something narrower still. Same underlying blocks, different menus.

Umbraco's out-of-the-box answer to "different menu" is "different data type." Follow that and you get one data type per document type:

```
Data Types/
├── Blog Article Blocks      (richText, image, code, callout)
├── Landing Page Blocks      (richText, image, code, callout, hero, slider, ctaBand, featureGrid, …)
├── Press Release Blocks     (richText, image, quote)
└── … one per document type that wants a different menu
```

Three problems show up fast:

- **It multiplies.** One data type per document type is the floor. This site is **multi-tenant** — several brands from one Umbraco instance — so if menus differ by brand too, you're into *document types × tenants* data types, almost all of them near-identical.
- **Adding a block is a chore, and a risky one.** Ship a new "video embed" block and you have to remember to add it to every data type that should offer it. Miss one and an editor quietly can't use it; the list drifts out of sync across data types that were supposed to match.
- **The Settings tree turns to soup.** Dozens of `… Blocks` data types differing only in a checkbox list is hard to scan and harder to reason about. Nobody can tell at a glance which ones are meant to be the same.

The blocks themselves aren't the duplication — the *menus* are. And a menu is data, not a schema. So model it as data.

One honest caveat up front: this pattern earns its keep *at scale*. If you've a handful of document types with stable menus and a single tenant, the default — a data type per document type — is less to set up and perfectly fine; reach for what follows when the menus start to multiply or drift apart.

## What we're building

Invert the relationship:

```
Data Types/
└── Site Blocks (Restricted)     ← ONE master: every block the site has

Document Types/
├── Blog Article    → uses "Site Blocks (Restricted)", rule: { richText, image, code, callout }
├── Landing Page    → uses "Site Blocks (Restricted)", rule: (none → all blocks)
└── Press Release   → uses "Site Blocks (Restricted)", rule: { richText, image, quote }
```

One data type holds the union of every block. Every document type reuses it. The per-consumer menu lives as a **restriction rule** keyed by document type and resolved at edit time, so each editor sees only their subset. Adding a block is one edit to the master; nothing downstream needs touching. The Settings tree has one block data type in it, not thirty.

Two pieces of machinery make this work, and each has its own tutorial:

- **Resolving** which subset applies to the node being edited — a rule attached to a document type, inherited down the content tree, cached. See [Configuration that inherits down the content tree](./content-tree-inherited-config.md).
- **Enforcing** that subset in the editor — a custom property editor that wraps the native block editor and filters its "add block" catalogue to the allowed set. See [Wrapping Umbraco's native block editor](../refinements/wrapping-umbraco-native-block-editor.md).

This article is just the modelling choice that makes both worth building.

## Walkthrough

The concrete setup is short; the depth is in the two linked tutorials.

### Step 1 — Build one master data type

In **Settings → Data Types**, create a single Block Grid data type and configure it with *every* block the site uses. (A "block" here is an Umbraco **element type** added to the data type; its alias — `calloutBlock`, `heroBlock` — is what a restriction rule lists later.) In this repo the data type uses the **Block Grid (Restricted)** property editor rather than the native one — same schema and configuration, just the wrapping UI from the refinement above. A site can keep a second master for Block List if it needs both shapes; the point is "shared masters," not literally one row.

### Step 2 — Point document types at it, don't fork it

Every document type that needs a block area uses that same master data type for its property. No `Blog Article Blocks` / `Landing Page Blocks` variants. When a document type wants a different menu, that's a *rule*, not a new data type.

### Step 3 — Express the menu as a rule

On a document type's **Blocks** tab (added by the Block Restrictions package), enable restrictions and tick the blocks that document type may use. That writes a rule keyed by the document type — stored as data, version-controllable as `umbraco/BlockRestrictions/{alias}.json`:

```json
{
  "DocumentTypeAlias": "blogArticle",
  "AllowedBlocks": ["calloutBlock", "codeBlock", "imageBlock", "richTextBlock"]
}
```

Leave a document type unrestricted and it offers the full master set (fail-open — no rule means every block is offered, never none). Rules resolve down the *content* tree, closest-ancestor-wins: a rule on a node applies to everything beneath it until a lower level overrides — which is how the *same* document type can present different menus under different tenant roots. The resolution mechanics are the [inherited-config tutorial](./content-tree-inherited-config.md).

### Step 4 — Let the editor enforce it

There's nothing to configure for this step — it happens automatically once the property uses the restricted editor. When an editor opens a page, the restricted property editor resolves the rule for that node and filters the "add block" catalogue down to the allowed set, while leaving the rest of the native editor untouched. That's the [wrapping refinement](../refinements/wrapping-umbraco-native-block-editor.md).

## Alternatives we considered

- **A data type per document type (the default).** The explosion described above. It's genuinely simpler for a handful of document types with stable menus, and it needs no custom code — if you have three document types and they never change, do this and move on. The pattern here earns its keep once the menus multiply (many document types, and especially many tenants) and start drifting out of sync.
- **A site root + bespoke data types per tenant.** For a multi-tenant build, giving each brand its own root *and* its own block data types multiplies the sprawl by the number of tenants and fights the whole point of running tenants from one instance. A shared master with per-document-type (and, via inheritance, per-tenant-root) rules gets the same per-brand menus without the duplication.
- **Umbraco block groups.** The native block editor lets you *group* blocks within a data type for tidier authoring. Useful, but orthogonal: groups organise one data type's full list — they don't hand different document types different lists. They sit happily inside the master data type; they just don't solve per-consumer narrowing.
- **No guardrail — just train editors.** Document "please don't use the slider on a blog post" and hope. Works until it doesn't; the value of the restriction is that the wrong block simply isn't offered, so the guidance is built into the UI rather than living in a wiki nobody reads.

## Trade-offs and known limits

- **It's a resolved rule, not a schema boundary.** All consumers share one data type and one storage format; the "separation" is a restriction applied at edit time, not a structural wall. If you genuinely need different *storage* per consumer (different value shapes, different server-side conversion), this pattern is the wrong tool — make different data types.
- **The master can get large.** One data type holding the union of every block is a long list to configure and reason about, even though each consumer sees a slice. Block groups (see Alternatives) help keep it navigable.
- **It buys flexibility with package complexity.** You're trading data-type sprawl for the resolution + editor-wrapping machinery, which isn't free out of the box. Past a threshold of document types it's a clear win; below it, the per-document-type data type is less to maintain.
- **Tightening a rule doesn't retroactively remove placed blocks.** Restricting a document type stops editors *adding* a block; instances already on existing pages keep rendering and stay editable (see the Grid/List behaviour in the [wrapping refinement](../refinements/wrapping-umbraco-native-block-editor.md)). That's deliberate — you don't want a settings change to silently orphan published content — but it means a restriction governs new additions, not historical ones.

## Where to go next

- **[Configuration that inherits down the content tree](./content-tree-inherited-config.md)** — how a rule attached to a document type resolves for any node, inherited by ancestor walk and cached.
- **[Wrapping Umbraco's native block editor](../refinements/wrapping-umbraco-native-block-editor.md)** — how the editor enforces the resolved subset without forking the native block editor.
- **[The `UmbracoCommunity.BlockRestrictions` README](../../../src/UmbracoCommunity.BlockRestrictions/README.md)** — the whole package end to end: workspace tab, dashboard, dual DB/JSON persistence, import/export.

If you've ever opened a Settings tree and found thirty `… Blocks` data types that differ by one checkbox, this is the pattern that collapses them back into one.
</content>

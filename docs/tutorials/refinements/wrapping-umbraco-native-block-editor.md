---
tags: [backoffice, property-editor, block-editor, clipboard]
---

# Wrapping Umbraco's native block editor with restriction filtering

> **Start with the why:** This refinement is the editor-UI *mechanics* of Block Restrictions. The reason the package exists — keeping **one master block data type** and narrowing it per document type, instead of building a data type per document type — is the content-modelling decision in [One master block data type, restricted per consumer](../foundations/one-master-block-datatype.md). Read that first if "why not just make another data type?" is on your mind. The other moving part, resolving *which* blocks are allowed for a node by walking the content tree, is [Configuration that inherits down the content tree](../foundations/content-tree-inherited-config.md); "the restriction" referred to throughout arrives from a server endpoint that does that walk. New to backoffice extensions in general? The [backoffice primer](../../primers/backoffice.md) is the five-minute orientation.

The Block List and Block Grid editors are two of the most useful things Umbraco ships, and by default they offer every block a data type is configured with to every editor, on every node. We wanted to narrow that list per document type (with inheritance down the content tree) — a "Blog Post" should only offer a handful of blocks, a "Landing Page" the full set. This tutorial is about the backoffice side of that: how to enforce the restriction in the editing UI *without* reimplementing the block editor, by wrapping the native one instead of replacing it. The sting in the tail is copy-and-paste, which breaks in a genuinely puzzling way once you wrap, and needs a little boilerplate to put right.

## The problem

A block restriction is only useful if the content editor can't add a disallowed block in the first place. That means the "add content" catalogue — the modal that pops up when you click *Add block* — has to show a filtered list. The catalogue builds that list from the data type's `blocks` configuration, which the property editor receives as a config collection at load time.

So the requirement is narrow: **show the native Block Grid / Block List, but with a `blocks` config that's been filtered down to the allowed set for the current node.** Everything else about the editor — rendering existing blocks, inline editing, areas, layout, settings, validation — should behave exactly as it does natively. We do not want to own any of that.

## Why the obvious fix doesn't work

There are two obvious fixes, and both have a sharp edge.

**Fork the native editor.** Copy Umbraco's `umb-property-editor-ui-block-grid` element and its block-manager machinery into your package, add the filter, register it under a new alias. This works for exactly one Umbraco version. The block editor is a large, fast-moving subsystem; the moment Umbraco improves it in a minor release, your fork is stale and you're re-merging hundreds of lines to get a bug fix. The whole appeal of "just filter the list" is lost if you have to maintain the editor too.

**Filter the config and pass it straight through.** Register a thin element, take the config Umbraco hands you, drop the disallowed blocks, hand it to a native element you render in your template. Closer — but two things bite:

1. **Shadow DOM cuts the cord.** A Lit element renders into a shadow root by default. The native block editor finds everything it needs (workspace context, property dataset, the variant being edited) through Umbraco's **context API** — its dependency-injection mechanism, where a provider sits high in the DOM and descendants reach it by firing events that *bubble up the tree*. A shadow boundary stops those events. The native element loads, then sits there inert because none of its context consumers can see their providers.
2. **The block manager caches the list on first config.** Even with the DOM sorted out, the native block manager reads the available block types **once**, from the first config it's given. Our restriction data arrives asynchronously (it's an authenticated API call). If the native element was already created with the unfiltered config, a later "here's the filtered config" is ignored — the catalogue still shows everything.

So the real shape of the solution is a wrapper that (a) lives in the Light DOM so context still propagates, and (b) is prepared to throw away and recreate the inner element when the restriction arrives late.

## Our approach

First, one piece of Umbraco vocabulary the rest of this leans on: a property editor is **two registrations**, not one. A *schema* (registered in C#) owns the storage format and the server-side value conversion; a *UI* (a Lit element) owns the editing experience. Normally they're a matched pair — "Block List" is a schema and its native UI together. They don't have to be.

So: register a **custom property editor UI** that reuses the **native schema**. The schema alias (`Umbraco.BlockGrid` / `Umbraco.BlockList`) keeps owning the storage format, the config structure, and the value conversion; only the *UI* alias is ours. Our editor therefore reads and writes byte-identical values to the native one — we've changed the chrome, not the data.

The UI element is a transparent proxy:

1. It renders into **Light DOM** (`createRenderRoot()` returns `this`) so Umbraco's context events propagate through it untouched.
2. It creates the native element **imperatively** (`document.createElement`) rather than in a template, so it can set complex object properties and control the element's lifecycle precisely.
3. It resolves the restriction from a server endpoint, **filters the `blocks` config**, and feeds the filtered config to the inner element — recreating that element if the data arrived too late to take effect.
4. It **re-dispatches** the inner element's value-change events as its own, so Umbraco's property system credits the change to the registered UI element (us), not the hidden inner one.
5. It **fails open**: if the API call fails, all blocks stay available. Better to show too many than to block editing entirely.

## Walkthrough

The real code lives in [`src/UmbracoCommunity.BlockRestrictions/Client/src/property-editors/`](../../../src/UmbracoCommunity.BlockRestrictions/Client/src/property-editors/). The snippets below are abridged from the Block Grid editor; the Block List one is the same shape with one deliberate difference, called out in Step 4.

### Step 1 — Register a UI that borrows the native schema

The manifest registers a `propertyEditorUi` but points `propertyEditorSchemaAlias` at the native editor. That one line is what keeps the value format identical:

```ts
{
  type: "propertyEditorUi",
  alias: "UmbracoCommunity.PropertyEditorUi.BlockGridRestricted",
  name: "Block Grid (Restricted)",
  elementName: "block-grid-restricted",
  js: () => import("./block-grid-restricted/block-grid-restricted.element.js"),
  meta: {
    label: "Block Grid (Restricted)",
    icon: "icon-layout",
    group: "lists",
    propertyEditorSchemaAlias: "Umbraco.BlockGrid", // ← native storage + config
    supportsReadOnly: true,
    settings: { properties: [ /* mirror the native data-type config fields */ ] },
  },
}
```

The `settings.properties` mirror the native editor's data-type configuration (grid columns, live editing, layout stylesheet, …) so the data type screen looks and behaves the same. A content architect picks "Block Grid (Restricted)" as the property editor on a data type and otherwise configures it exactly as they would the native one.

> This is one of *six* manifests this editor needs (before the clipboard translators in Step 5) — the UI plus clipboard and sort-mode contexts and actions. They exist because the native contexts and actions are scoped with `forPropertyEditorUis` — a manifest field that pins a registration to specific UI aliases. The native ones list only the native aliases, so they skip ours, and we re-register equivalents pointed at our alias; [the backoffice primer](../../primers/backoffice.md#registering-a-piece-of-ui) has the full list. Step 5 covers the clipboard half, which is the part with real logic in it.

### Step 2 — Wrap the native element in Light DOM

The element extends `UmbElementMixin(LitElement)`, implements `UmbPropertyEditorUiElement`, and — critically — opts out of Shadow DOM:

```ts
@customElement("block-grid-restricted")
export default class BlockGridRestrictedElement
  extends UmbElementMixin(LitElement)
  implements UmbPropertyEditorUiElement
{
  // Render into Light DOM so Umbraco's context events still bubble through us.
  protected override createRenderRoot() {
    return this;
  }
```

`UmbElementMixin` is what adds Umbraco's context API — `consumeContext` and `observe`, both used heavily in Step 3 — onto the standard `LitElement`. `UmbPropertyEditorUiElement` is the contract Umbraco expects of a property editor UI (a `value`, a `config`, a `readonly`).

There's a subtlety in *getting* the native element to exist. Umbraco lazy-loads property editor UI modules — the native block grid module only loads when something uses its alias. Since our data type uses *our* alias, the native module may never have been requested, and a naïve `customElements.whenDefined("umb-property-editor-ui-block-grid")` hangs forever (most visible on a hard reload). So we nudge the extension registry to load the native module first, then wait:

```ts
async connectedCallback() {
  super.connectedCallback();
  const tagName = "umb-property-editor-ui-block-grid";

  if (!customElements.get(tagName)) {
    const manifest = umbExtensionsRegistry.getByAlias("Umb.PropertyEditorUi.BlockGrid") as any;
    if (manifest?.element) await manifest.element(); // run the lazy import
    await customElements.whenDefined(tagName);
  }

  if (this.isConnected) this._createInnerElement();
}
```

`_createInnerElement()` builds the native element by hand, wires up value proxying, and pushes the current config and value into it:

```ts
private _createInnerElement() {
  this._innerElement?.remove();                 // recreate-safe
  this._innerElement = document.createElement("umb-property-editor-ui-block-grid");

  // The inner editor reports edits as `property-value-change`. Re-dispatch from us,
  // with bubbles + composed, so Umbraco's property system sees *this* element change.
  this._innerElement.addEventListener("property-value-change", (e: Event) => {
    this.value = (e as CustomEvent).detail.value;
    this.dispatchEvent(new CustomEvent("property-value-change", { bubbles: true, composed: true }));
  });

  this._innerReady = true;
  (this._innerElement as any).config = this._effectiveConfig ?? this._originalConfig;
  (this._innerElement as any).value = this.value;
  this.requestUpdate();   // Lit inserts the live DOM node via `render()`
}
```

The template just drops that live node in (Lit happily renders a DOM element, not only strings), with a loader while it's being built:

```ts
render() {
  return html`${this._innerElement ?? html`<uui-loader></uui-loader>`}`;
}
```

### Step 3 — Resolve the restriction (and only in the right place)

The element consumes four contexts in the constructor. They resolve in any order, so each one calls a shared `_tryLoad()` guard that fires the API call once all the prerequisites are present:

| Context | What it gives us | Why |
| --- | --- | --- |
| `UMB_AUTH_CONTEXT` | OpenAPI config (token, base URL) | Configures the API client for the authenticated call |
| `UMB_ENTITY_CONTEXT` | the node's `unique` (GUID) | The key the server resolves restrictions against |
| `UMB_DOCUMENT_WORKSPACE_CONTEXT` | content-type key; *and its mere presence* | Only exists in the **Content** section — confirms we're editing content, not previewing a doc type in Settings |
| `UMB_PARENT_ENTITY_CONTEXT` | parent node key | Fallback for **new** content, where the node has no tree position yet |

```ts
// Auth: hand the token getter to our API client, then mark auth ready.
this.consumeContext(UMB_AUTH_CONTEXT, (authContext) => {
  if (!authContext) return;
  const config = authContext.getOpenApiConfiguration();
  setAuthConfig({ token: config?.token, baseUrl: config?.base });
  this._authReady = true;
  this._tryLoad();
});

// Entity: observe (not read once) — the same editor instance can be reused for another node.
this.consumeContext(UMB_ENTITY_CONTEXT, (context) => {
  if (!context) return;
  this.observe(context.unique, (unique) => {
    if (unique) { this._entityKey = unique; this._tryLoad(); }
  });
});

private _tryLoad() {
  if (this._authReady && this._entityKey && this._isContentContext) {
    this._loadRestrictions();
  }
}
```

(`setAuthConfig` and `getAllowedBlocks` come from the package's own typed API client — a thin fetch wrapper over the secured `/allowed-blocks/{nodeKey}` backoffice endpoint that does the tree walk. The [backoffice primer](../../primers/backoffice.md#talking-to-the-c-apis) covers that auth-token-to-fetch handover.)

The `_isContentContext` check (set when `UMB_DOCUMENT_WORKSPACE_CONTEXT` resolves) is what stops the editor firing API calls when it's instantiated outside content editing. The fetch itself passes the content-type and parent keys as fallback context so the server can resolve restrictions for a node that doesn't exist in the tree yet:

```ts
private async _loadRestrictions() {
  try {
    this._restrictionInfo = await getAllowedBlocks(this._entityKey, this._contentTypeKey, this._parentKey);
  } catch (error) {
    console.error("Failed to load block restrictions, showing all blocks:", error);
    this._restrictionInfo = null;     // fail open
  }
  this._applyRestrictions();

  // The recreate-on-restriction pattern: if the inner element already exists with the
  // unfiltered config, the block manager has cached the full list — rebuild it.
  if (this._innerReady && this._restrictionInfo?.hasRestrictions) {
    this._createInnerElement();
  }
}
```

That last `if` is the whole reason the wrapper is built imperatively. The block manager won't re-read its block list, so when the restriction lands late we throw the inner element away and make a new one against the filtered config.

### Step 4 — Filter the config

`_applyRestrictions()` rebuilds the config collection with a filtered `blocks` entry and everything else passed through. It builds a fresh `UmbPropertyEditorConfigCollection` (via the original's own constructor) so Lit's change detection treats it as a new value:

```ts
const allowedKeys = new Set(
  this._restrictionInfo.allowedContentElementTypeKeys.map((k) => k.toLowerCase())
);

const configValues = [];
for (const entry of this._originalConfig) {
  if (entry.alias === "blocks") {
    const blocks = entry.value as Array<{ contentElementTypeKey?: string; [k: string]: unknown }>;
    const next = blocks.map((block) => {
      if (!block.contentElementTypeKey) return block;
      if (allowedKeys.has(block.contentElementTypeKey.toLowerCase())) return block;
      // Block Grid: keep the definition, just forbid adding it.
      return { ...block, allowAtRoot: false, allowInAreas: false };
    });
    configValues.push({ alias: "blocks", value: next });
  } else {
    configValues.push({ alias: entry.alias, value: entry.value });
  }
}
this._effectiveConfig = new (this._originalConfig.constructor as any)(configValues);
```

Block List is the one deliberate difference promised earlier. Where Grid *maps* over the blocks and disables the disallowed ones, List just *filters* them out:

```ts
// Block List: drop disallowed blocks from the array entirely.
const next = blocks.filter(
  (block) => !block.contentElementTypeKey || allowedKeys.has(block.contentElementTypeKey.toLowerCase())
);
```

**The Grid / List asymmetry.** Block Grid keeps every block definition and merely sets `allowAtRoot: false` / `allowInAreas: false` on disallowed ones; Block List removes them. The difference is deliberate: a Block Grid's layout and areas reference block definitions by key, so removing a definition outright would break the rendering of any block an editor added *before* the restriction existed. Disabling the "allow" flags keeps those existing blocks renderable and editable while removing them from the catalogue — so a restriction tightens what you can *add* without orphaning what's already there. The flatter Block List doesn't have that layout-reference concern, so it takes the simpler filter.

### Step 5 — Keep copy-and-paste alive

Here's the bug that's genuinely fun to track down: with the wrapper working, copy a block from a restricted editor, paste it into a native one (or vice versa), and the paste is silently rejected. Nothing in the console, the block just doesn't appear.

The cause: Umbraco's clipboard system uses **value translators** to convert between a property editor's value format and a neutral clipboard format. The native block editors register translators scoped to the *native* UI aliases via `forPropertyEditorUis`. Our editor has a different alias, so those translators don't apply to it — copy/paste has no translator and quietly no-ops.

The fix is to register our own translators (and the copy/paste actions and contexts that surface them). You can't import the native translator classes to do it, either: the backoffice loads `@umbraco-cms/*` from a shared browser **import map** at runtime, and our Vite build leaves those imports un-bundled to use it. The native translators live at internal `dist-cms` paths that aren't *in* that import map, so importing them throws at runtime. So we **re-implement them against the public API** (`UmbControllerBase` from `@umbraco-cms/backoffice/class-api`). Because our editor uses the native value format, the translation is the same handful of transforms the native one does — extract the layout from under the schema-alias key and strip the `$type` that .NET serialisation adds:

```ts
// copy: Block List value → neutral "block" clipboard format
class BlockListToBlockCopyTranslator extends UmbControllerBase {
  async translate(propertyValue: any) {
    const v = structuredClone(propertyValue);
    const layout = v.layout?.["Umbraco.BlockList"];
    layout?.forEach((item: any) => delete item.$type);
    return { contentData: v.contentData ?? [], layout, settingsData: v.settingsData ?? [] };
  }
}
export { BlockListToBlockCopyTranslator as api };
```

The paste translator does the reverse — wraps the layout back under the schema alias — and adds an `isCompatibleValue` check so the editor won't accept a paste whose blocks aren't in the (now restricted) allowed set:

```ts
async isCompatibleValue(propertyValue: any, config: any): Promise<boolean> {
  const allowed = config.find((c: any) => c.alias === "blocks")?.value.map((b: any) => b.contentElementTypeKey) ?? [];
  const incoming = propertyValue.contentData.map((c: any) => c.contentTypeKey);
  return incoming.every((b: string) => allowed.includes(b));
}
```

Block Grid needs two clipboard formats (`block` and `gridBlock`), so it gets two copy and two paste translators; Block List needs one of each. With the translators and the copy/paste/sort actions registered against our alias, the three-dot menu lights up and clipboard round-trips work in both directions.

## Alternatives we considered

- **Filter server-side by replacing the block catalogue modal.** This is what [Kraftvaerk.Umbraco.BlockFilter](https://github.com/kraftvaerk/kraftvaerk.umbraco.blockfilter) does, and it's a genuinely different design: rather than wrap the property editor, it swaps the native *catalogue modal* and fires a server-side `RemodelBlockCatalogueNotification` whose handler trims the block list before the UI renders. That puts the decision in C# and makes it easy to key on user group — something our editor-side approach doesn't do. We went the other way because our rules are *by content position*: they key on the node and inherit down the tree, which BlockFilter (keyed by document-type alias, no tree inheritance) doesn't. Neither is strictly better; the right choice follows from whether your rules are "by user group" or "by where the node sits". (Both fail open when no rule matches.)
- **Two custom data types instead of a wrapper.** Configure a separate restricted data type per document type with a hand-picked block list. No code at all — but it explodes into a data type per page type, none of them inherit, and editors can't see *why* a block is missing. It doesn't scale past a handful of types.
- **A backoffice JS override that mutates the catalogue in place.** Patch the rendered modal's DOM or monkeypatch the block manager. Fragile against every backoffice release and invisible to anyone reading the extension manifest; we wanted the restriction to be a first-class registered editor, not a runtime patch.

## Trade-offs and known limits

- **It depends on the native element's contract.** We rely on `umb-property-editor-ui-block-grid` existing, accepting `config`/`value` properties, and emitting `property-value-change`. These are stable, public-ish surfaces, but they're not a versioned API — an Umbraco major could move them, and the recreate/proxy plumbing would need revisiting. This is the cost of wrapping; the upside is we inherit every other block-editor improvement for free.
- **Recreate-on-restriction can flash.** When the restriction arrives after first paint, we destroy and rebuild the inner editor, which is a visible blink on a slow connection. In practice the API is fast and cached, so it's rare — but it's the reason the editor shows a `<uui-loader>` rather than an unfiltered editor while it resolves.
- **Restrictions are an editing-time guardrail, not server enforcement.** Filtering the catalogue stops editors *adding* disallowed blocks; it does not stop a determined API client from posting one. If you need a hard guarantee, pair this with server-side validation on save. (For our use — guiding content editors — the guardrail is the point.)
- **The clipboard translators are a maintenance tax.** They're replicas of native behaviour; if Umbraco changes its clipboard value shape, ours drift and need updating. They're covered by unit tests in [`translators/*.test.ts`](../../../src/UmbracoCommunity.BlockRestrictions/Client/src/property-editors/translators/) precisely because they're the kind of code that breaks quietly.

## Where to go next

- The restriction *resolution* side — how the server turns a node key into an allowed-block set by walking the content tree and caching the result — is the [content-tree-inherited config](../foundations/content-tree-inherited-config.md) foundation.
- How those rules are version-controlled and moved between environments (database, version-controllable JSON, and a zip path for Cloud) is [syncing config across environments](./syncing-config-across-environments.md).
- For the package as a whole — workspace view, dashboard, EF Core migrations, import/export — the [`UmbracoCommunity.BlockRestrictions` README](../../../src/UmbracoCommunity.BlockRestrictions/README.md) is the end-to-end tour.

If you've ever stared at a silently-failing paste wondering why the block just won't appear — now you know which translator went missing.
</content>

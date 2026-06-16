---
tags: [content-tree, configuration, inheritance, caching]
---

# Configuration that inherits down the content tree

A common need in a CMS: attach a piece of configuration to *something* high up, and have everything below it pick the value up automatically unless it's overridden closer to home. Block Restrictions does it with allowed-block rules — set a rule once, and every page beneath inherits it — but the shape is general. This tutorial is the resolution engine behind that: how to attach a rule, resolve it for any node by walking *up* the tree, cache the answer so you're not re-walking on every request, and fail open when nothing is configured. It's the "how rules resolve" half of the [Block Restrictions trio](./one-master-block-datatype.md); the [editor-wrapping refinement](../refinements/wrapping-umbraco-native-block-editor.md) is what *consumes* the answer.

## Why you might want this

"Configure on a parent, apply to descendants" turns up all over a CMS:

- **Permissions** — grant a role access to a section and let it cascade.
- **Feature flags** — switch a feature on for a branch of the site.
- **Theming** — set a brand colour on a landing section, inherited by its children.
- **Our case** — restrict which blocks an editor may add, set per document type but applied per page.

The tempting shortcut is to resolve against the **document-type** hierarchy (compositions, inherited types) — Umbraco already models that. But it answers the wrong question. A document type tells you *what a node is*, not *where it sits*. We want "a Blog Post **under the Marketing root** offers these blocks; the same Blog Post type **under the Press root** offers those" — and that distinction lives in the **content tree**, not the type system. So the resolution has to walk content ancestors (parent nodes), and the same document type can resolve to different config depending on where it appears.

## What we're building

A resolver that, given a content node, returns its effective configuration:

1. Look the node up; check its document type for a rule.
2. No rule? Walk up to the parent node and check again.
3. First rule found wins — flag whether it came from the node itself or an ancestor.
4. No rule anywhere up the tree? **Fail open** — return "no restriction", which the caller treats as "allow everything".

Plus two cache layers so a page with several restricted editors doesn't trigger several tree walks, and a cheap way to invalidate *everything* the instant any rule changes.

The whole thing lives in [`BlockRestrictionService`](../../../src/UmbracoCommunity.BlockRestrictions/BlockRestrictionService.cs) and [`BlockRestrictionStore`](../../../src/UmbracoCommunity.BlockRestrictions/Infrastructure/BlockRestrictionStore.cs).

## Walkthrough

### Step 1 — Attach the rule to the document type

A rule is keyed by document-type GUID and stores the allowed block **aliases** (not GUIDs):

```json
{ "DocumentTypeAlias": "blogArticle", "AllowedBlocks": ["calloutBlock", "imageBlock", "richTextBlock"] }
```

(One vocabulary note before we go further: Umbraco's UI calls these *document types*, but its API calls them *content types* — the same thing — which is why the resolver below reaches for `IContentTypeService`.)

Storing aliases rather than element-type keys is a deliberate choice — aliases are human-readable and produce clean git diffs in the JSON files (see the [dual-persistence refinement](../refinements/dual-persistence-db-and-json.md)). They're resolved at read time to **element-type** GUIDs (the content type behind each block) with a single batched lookup — where `aliasSet` is the rule's allowed aliases as a case-insensitive set — so the per-alias cost stays flat:

```csharp
var lookup = _contentTypeService.GetAll()
    .Where(ct => ct.IsElement && aliasSet.Contains(ct.Alias))
    .ToDictionary(ct => ct.Alias, ct => ct.Key, StringComparer.OrdinalIgnoreCase);
```

### Step 2 — Resolve by walking the content tree

This is the heart of it — the body of the public `ResolveAllowedBlocksForNodeAsync` (the method Step 5 re-enters on a parent node). Start at the node, check its type for a rule, and if there isn't one, climb to the parent and try again — until a rule turns up or you run out of ancestors:

```csharp
var content = _contentService.GetById(nodeKey);
if (content == null) return null;          // node doesn't exist yet — see Step 5

var current = content;
var inherited = false;

while (current != null)
{
    var contentType = _contentTypeService.Get(current.ContentTypeId);
    if (contentType == null) break;

    var rule = await _store.GetByDocumentTypeKeyAsync(contentType.Key);
    if (rule != null)
        return Build(rule, contentType, inheritedFromAncestor: inherited);  // first rule wins
                                        // Build packages the response, resolving aliases → GUIDs as in Step 1

    if (current.ParentId > 0)               // climb the CONTENT tree, not the type hierarchy
    {
        current = _contentService.GetById(current.ParentId);
        inherited = true;
    }
    else break;
}
```

The one line that matters most is `current = _contentService.GetById(current.ParentId)`. We resolve the type for the rule *lookup*, but we move via `ParentId` — content ancestry. (Umbraco content carries both an integer `Id`/`ParentId` and a GUID `Key`; we enter the resolver by `Key` but climb by the integer `ParentId`, where `ParentId > 0` simply means "has a real parent" — the root's parent isn't a positive id.) That's what makes "same type, different place, different config" work, and it's the single thing most often implemented wrong — by walking the document-type hierarchy (the compositions and inherited types Umbraco stitches a type out of) instead of the content tree.

"First rule wins" gives you override-by-proximity for free: a rule on a deep node beats one higher up, because the walk hits the closer one first.

### Step 3 — Fail open when nothing matches

Fall out of the loop with no rule found and you return "no restriction" rather than "nothing allowed":

```csharp
return new AllowedBlocksResponse { HasRestrictions = false };
```

This is the safety property that makes the feature droppable into an existing site: install it, configure nothing, and every editor behaves exactly as before. Restrictions are opt-in per branch, not opt-out. (Pick the fail direction that's *safe* for your domain — for permissions you'd usually fail *closed*; for an editor convenience like this, open is right.)

### Step 4 — Cache the walk (and invalidate it cheaply)

The walk is several `GetById` calls deep, and a page with multiple restricted editors would repeat it per editor. Two cache layers, each solving a different problem:

**The store** (`BlockRestrictionStore`) **caches individual rules** per document-type key (30-minute sliding expiration), so the repeated `GetByDocumentTypeKeyAsync` calls across one walk — and across walks — don't hit the database each time. It's invalidated per key on write:

```csharp
_cache.Remove($"BlockRestriction_{documentTypeKey}");   // on upsert / delete
```

**The service caches the fully resolved result** per node (60-second absolute expiration), so a cache hit skips the whole walk *and* the alias resolution. The interesting part is invalidation. Editing one rule can change the answer for *every descendant node that inherits it* — you can't know which cached nodes are affected without walking the tree you were trying to avoid. So instead of evicting selectively, bake a generation counter into the cache key and bump it:

```csharp
var version = Interlocked.Read(ref _ruleVersion);
var cacheKey = $"BlockRestriction_Resolved_{version}_{nodeKey}";
// ... on any rule save/delete:
Interlocked.Increment(ref _ruleVersion);   // every old key is now unreachable; entries expire by TTL
```

Incrementing `_ruleVersion` makes every previously-cached key unreachable in one cheap atomic operation — no enumeration, no tracking which nodes depend on which rule. The stale entries simply age out via their TTL. It's a neat trick any time a single write can invalidate an unknowable set of cached reads.

### Step 5 — The new-content edge

There's one case the tree walk can't handle: **content being created**. The node has no GUID in the tree yet, so `GetById` returns null. The resolver falls back to the keys the editor *can* supply — the new node's document type and its intended parent:

```csharp
// contentTypeKey: check the new type for a direct rule first
// parentKey: failing that, resolve against the parent node (which IS in the tree)
if (contentTypeKey.HasValue) { /* direct rule on the type being created */ }
if (parentKey.HasValue) return await ResolveAllowedBlocksForNodeAsync(parentKey.Value);
```

The API endpoint tries the node first and only falls back when it 404s and a type/parent is supplied — so restrictions apply while you're *creating* a page, not only once it's saved.

## Alternatives we considered

- **Resolve against the document-type hierarchy.** Use compositions / type inheritance instead of the content tree. Simpler (no walk — Umbraco hands you the type chain), but it can't express "same type, different config by location", which was the whole requirement. If your config genuinely is a property of the *type* and never the *place*, this is the better, cheaper choice — put the config on a composition and be done.
- **Evict cached nodes individually on a rule change.** Track which nodes resolved from which rule and evict precisely. Correct, but you'd maintain a reverse index and still face the "this rule has thousands of descendants" fan-out. The generation counter gets the same correctness for a single `Interlocked.Increment`.
- **No resolved cache — just walk every time.** The store cache alone keeps it off the database, so this isn't *slow*, but a deep tree × several editors per page × every content load adds up. The 60-second resolved cache turns a repeated walk into one walk per node per minute.
- **Config in `appsettings` or code.** Fine for values that change at deploy time, wrong for something content editors set in the backoffice and expect to take effect immediately. This belongs in data, attached to the tree, editable by the people who manage content.

## Trade-offs and known limits

- **Cold resolution is O(tree depth).** The first lookup for a node does up to *depth* `GetById` calls. In practice trees are shallow and the result is cached for 60 seconds, so the cost is paid rarely; on a pathologically deep tree you'd feel the cold walk.
- **The caches are per-process.** Both layers use in-memory `IMemoryCache`. On a single instance, a rule edit is reflected immediately (the counter bump invalidates the resolved cache; the per-key `Remove` clears the store cache). On a **scaled-out** deployment, an instance that didn't handle the write keeps serving its cached answer until expiry — up to 60 seconds for a resolved result, up to the 30-minute sliding window for a rule. For an editing-time convenience that's an acceptable lag; if you needed cluster-wide instant consistency, you'd hook the writes into Umbraco's distributed cache refreshers instead of a bare `IMemoryCache`.
- **It resolves "what's allowed", it doesn't enforce.** This service answers a question; making the editor obey the answer (and failing open if the call errors) is the [wrapping refinement's](../refinements/wrapping-umbraco-native-block-editor.md) job, and it isn't a server-side guarantee against a crafted API request.
- **Aliases must resolve.** Rules store aliases; if an element type is renamed or deleted, the alias no longer maps to a key and is dropped from the resolved set (with a warning logged). That's the cost of the human-readable, git-friendly storage format — worth it here, but know that renames need a rule update.

## Where to go next

- **[One master block data type, restricted per consumer](./one-master-block-datatype.md)** — the content-modelling reason this resolver exists.
- **[Wrapping Umbraco's native block editor](../refinements/wrapping-umbraco-native-block-editor.md)** — how the resolved answer is enforced in the editing UI.
- **[The `UmbracoCommunity.BlockRestrictions` README](../../../src/UmbracoCommunity.BlockRestrictions/README.md)** — the whole package, including the dual DB/JSON persistence the rule store sits on.
- If your version of this pattern is tenant-shaped rather than branch-shaped, the [multi-tenant content resolution](./multi-tenant-content-resolution.md) foundation walks the same tree for a different purpose.

The walk-up-and-cache shape is worth keeping in your back pocket — the next time you need "set it once up here, inherit it everywhere below", you've already got the engine.
</content>

# Block Restrictions for Umbraco

## The problem

On large Umbraco sites with many block types, content editors are presented with the full catalogue of blocks when editing any page. This creates noise — a homepage might need hero banners and featured content grids, while a blog post only needs rich text and image blocks. Editors waste time scrolling through irrelevant options, and there's a risk of adding blocks that don't belong on a particular page type.

## What Block Restrictions does

Block Restrictions lets you control which block types are available on a per-document-type basis, with automatic inheritance down the content tree. Restrictions are configured in the backoffice by developers or senior editors, and enforced transparently when editors work with content.

- **Restrict by document type** — define which blocks are allowed for each document type (e.g. "Homepage" gets hero + grid blocks, "Blog Post" gets text + image blocks)
- **Inheritance** — restrictions cascade down the content tree. Set them on a parent document type, and all child pages inherit the same rules automatically
- **Override at any level** — a child document type can define its own restrictions, overriding what it inherits from ancestors
- **Fail-open** — if no restrictions are configured at any level, all blocks are available (existing behaviour)

## How it works

### For developers: setting up restricted data types

1. **Create a restricted data type** — In Settings > Data Types, create a new data type using **Block Grid (Restricted)** or **Block List (Restricted)** as the property editor. These are drop-in replacements for the native Block Grid / Block List editors — they share the same schema and support all the same configuration options (block types, grid columns, layout stylesheet, live editing, etc.)

2. **Assign to document types** — Use the restricted data type on your document type properties, just as you would the native block editors. The simplest approach is often to duplicate an existing Block Grid/List data type and change its property editor to the restricted variant, preserving all your block type configuration.

3. **Existing content is unaffected** — Switching a property to the restricted editor doesn't alter stored content. Blocks already on a page continue to render correctly. The restriction only controls what *new* blocks can be added.

### For developers/editors: configuring restrictions

1. **Navigate to the Blocks tab** — Open any document type in Settings. You'll see a **Blocks** tab (with a filter icon) in the workspace.

2. **Enable restrictions** — Toggle on "Restrict available blocks." When off, the document type inherits restrictions from ancestor content nodes.

3. **Select allowed blocks** — A checklist shows all available element types. Tick the ones that should be available when editing content of this document type. Use the tools to help:
   - **Filter by data type** — narrow the list to only show blocks configured on a specific restricted Block Grid or Block List data type
   - **Text search** — filter by block name or alias
   - **Select all / Deselect all** — bulk actions scoped to the current filter
   - **Show settings types / Show composition types** — toggles to show or hide element types prefixed with "settings" or "composition" (hidden by default to reduce clutter)

4. **Save** — Click "Update" to save the restriction rules. Changes take effect immediately for content editors.

### For content editors: what changes

When editing a page that has block restrictions (directly or inherited):

- **The "Add content" picker** only shows the allowed block types — editors can't accidentally add blocks that don't belong
- **Existing blocks are preserved** — if a page already has blocks that are later excluded by a restriction, they remain visible and editable. The restriction only limits new additions.
- **Copy, paste, and sort mode** — the restricted editors support the same three-dot context menu actions as the native block editors (copy to clipboard, paste from clipboard, enter sort mode)
- **New content pages** — restrictions apply immediately when creating new content, not just when editing existing pages. The system uses the document type and parent node to resolve restrictions before the content is saved for the first time.
- **No restrictions configured?** — the editor works exactly like the standard block editor with all blocks available

### Inheritance in practice

Restrictions follow the content tree, not the document type hierarchy:

| Content node | Document type | Has own restriction? | Effective restriction |
|---|---|---|---|
| Home | homePage | Yes — allows Hero, Grid, CTA | Hero, Grid, CTA |
| Home > About | contentPage | No | Inherits from Home: Hero, Grid, CTA |
| Home > About > Team | teamPage | Yes — allows Team Grid, Bio Card | Team Grid, Bio Card |
| Home > Blog | blogLanding | No | Inherits from Home: Hero, Grid, CTA |

- **About** has no restrictions of its own, so it inherits Home's rules
- **Team** defines its own restrictions, which override the inherited ones entirely
- **Blog** also inherits from Home

To **remove** a restriction and fall back to inheritance, toggle off "Restrict available blocks" and click Update.

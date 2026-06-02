# Block Restrictions — Roadmap

## Configurable hidden-type prefixes

**Current behaviour**: The workspace view hides element types whose alias starts with `settings` or `composition` behind toggles. These prefixes are hardcoded in the frontend (`block-restrictions.element.ts`).

**Proposed change**: Allow developers to configure hidden prefixes via `appsettings.json`:

```json
"BlockRestrictions": {
  "HiddenAliasPrefixes": ["settings", "composition"]
}
```

- Add a configuration options class bound to the `BlockRestrictions` section
- Expose the prefixes via a new API endpoint (e.g. `GET /config/hidden-prefixes`)
- Frontend fetches the prefixes on load and uses them to build the visibility toggles dynamically
- Default to `["settings", "composition"]` when no configuration is present, preserving current behaviour

This lets teams with different naming conventions (e.g. `_settings`, `shared`, `internal`) customise the workspace view without code changes.

---

## Per-property restrictions

**Current behaviour**: Restrictions are set per document type. All block grid/list properties on that document type share the same restriction.

**Proposed change**: Allow restrictions to be scoped to a specific property on a document type, so a "Main Content" block grid and a "Sidebar" block list on the same document type can have different allowed blocks.

- Extend the database schema to add an optional `PropertyAlias` column (nullable — `null` means "applies to all properties")
- The property editors pass their own property alias when calling `allowed-blocks`, and the API resolves the most specific match (property-level rule > document-type-level rule > inherited)
- The workspace view would need a property picker or grouped UI to configure per-property rules

This is the most impactful enhancement but also the most complex — it changes the data model and workspace view significantly.

---

## Restriction audit dashboard

A backoffice dashboard showing an overview of all configured restrictions:

- Table of document types with active restrictions, showing how many blocks are allowed and when the rule was last updated
- Highlight document types that have restrictions but where some allowed aliases no longer resolve to existing element types (stale rules)
- Show the effective inheritance chain for a given content node
- Export restrictions as JSON for documentation or migration purposes

The backend already has `GetAllAsync()` on the store, so the data access layer is partially in place.

---

## Notification on stale restrictions

When element types are renamed or deleted, existing restriction rules that reference them by alias become stale — the allowed alias no longer resolves to a content type.

- Add a notification handler on `ContentTypeSavedNotification` and `ContentTypeDeletedNotification`
- When an element type alias changes, check all stored rules for the old alias and either auto-update or surface a warning in the backoffice
- When an element type is deleted, flag affected rules

This leverages the alias-based storage design and makes it more robust.

---

## Deny-list mode

**Current behaviour**: Restrictions use an allow-list — you tick which blocks *are* allowed, everything else is hidden.

**Proposed change**: Add a deny-list option — tick which blocks should be *excluded*, everything else is allowed.

- Add a `Mode` column to the rule (`Allow` or `Deny`)
- The service inverts the logic when resolving: deny-list removes the specified aliases from the full set
- Useful when you have many block types and only want to exclude a few

---

## Caching improvements

**Current behaviour**: Two-tier caching is in place: store-level (30-minute sliding per document type key) and service-level (60-second absolute per content node key with version-based invalidation). Browser response caching provides a third tier (60 seconds on `allowed-blocks`).

**Potential improvements**:
- Add output caching on the `allowed-blocks` endpoint using Umbraco's `[OutputCache]` for server-side caching, complementing the existing client-side response caching
- Consider a distributed cache (`IDistributedCache`) for load-balanced environments
- Pre-warm the cache for frequently accessed content nodes during backoffice startup

---

## Block List preservation of non-allowed blocks

**Current behaviour**: Block Grid keeps all block type definitions and uses `allowAtRoot: false` / `allowInAreas: false` to hide non-allowed types from the picker while still rendering existing blocks. Block List has no equivalent mechanism, so non-allowed types are filtered from the config entirely. This means if a block was previously added and is later restricted, it won't render in the editor.

**Proposed change**: Investigate rendering restricted Block List items in a read-only or visually distinct state rather than hiding them completely. This may require patching the Block List's entry rendering or creating a custom entry element that detects restricted types and renders a placeholder.

---

## NuGet package distribution

Package the project for distribution as a NuGet package so other Umbraco sites can install it without copying source code:

- Add package metadata to the `.csproj` (id, version, description, authors, license)
- Configure static web assets so the built frontend is included in the package
- Add a `README.md` for NuGet gallery display
- Publish to NuGet.org or a private feed

---

## Automated tests

- **Backend unit tests**: Service logic (tree walking, alias resolution, fail-open behaviour), store operations, controller responses
- **Backend integration tests**: EF Core migrations on SQLite, full API round-trips with an in-memory Umbraco test host
- **Frontend tests**: Restriction application logic (config transformation for both block grid and block list), API client mocking, workspace view state management

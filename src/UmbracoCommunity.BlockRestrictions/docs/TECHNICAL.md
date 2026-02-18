# Block Restrictions — Technical Documentation

## Project overview

`UmbracoCommunity.BlockRestrictions` is a self-contained Umbraco package that adds per-document-type block restriction rules with content tree inheritance. It consists of a .NET backend (API, service, EF Core storage) and a TypeScript/Lit frontend (workspace view, two property editor wrappers).

The package is structured as a standalone project within the solution at `src/UmbracoCommunity.BlockRestrictions/`.

## Project structure

```
UmbracoCommunity.BlockRestrictions/
├── BlockRestrictionComposer.cs        # Umbraco IComposer — DI, EF Core, Swagger
├── BlockRestrictionService.cs         # Business logic — restriction resolution, element type queries
├── Constants.cs                       # API name constant
├── Controllers/
│   ├── BlockRestrictionApiControllerBase.cs  # Base controller — auth, routing, API versioning
│   └── BlockRestrictionApiController.cs      # Endpoints — rules CRUD, allowed blocks, element types
├── Infrastructure/
│   ├── BlockRestrictionEntity.cs             # EF Core entity
│   ├── BlockRestrictionDbContext.cs           # DbContext with table mapping
│   ├── BlockRestrictionDbContextFactory.cs    # Design-time factory for EF migrations
│   ├── BlockRestrictionMigrationHostedService.cs  # Auto-applies migrations on startup
│   └── BlockRestrictionStore.cs              # Data access layer with IMemoryCache
├── Migrations/                        # EF Core migrations (SQLite + SQL Server)
├── Models/
│   ├── AllowedBlocksResponse.cs       # Response DTO for restriction resolution
│   ├── BlockRestrictionRuleDto.cs     # Rule DTO (document type key + allowed aliases)
│   ├── ElementTypeInfo.cs             # Element type DTO (key, alias, name, icon)
│   ├── BlockGridDataTypeInfo.cs       # Block data type DTO (key, name, content element type keys)
│   └── SaveBlockRestrictionRequest.cs # Request DTO for saving rules
├── Client/                            # Frontend — TypeScript/Lit/Vite
│   ├── package.json
│   ├── tsconfig.json
│   ├── vite.config.ts
│   └── src/
│       ├── bundle.manifests.ts        # Aggregates all extension manifests
│       ├── api/
│       │   └── client.ts             # API client — fetchWithAuth, typed functions
│       ├── workspace-views/
│       │   ├── manifest.ts           # Workspace view manifest (Blocks tab on document types)
│       │   └── block-restrictions/
│       │       └── block-restrictions.element.ts  # Configuration UI
│       └── property-editors/
│           ├── manifest.ts           # Property editor + context + action manifests
│           ├── block-grid-restricted/
│           │   └── block-grid-restricted.element.ts  # Block Grid wrapper
│           └── block-list-restricted/
│               └── block-list-restricted.element.ts  # Block List wrapper
└── wwwroot/
    └── App_Plugins/UmbracoCommunityBlockRestrictions/  # Built frontend output
```

## Backend

### Composer (`BlockRestrictionComposer.cs`)

Implements `IComposer` to register all services on startup:

- **EF Core DbContext factory** — configured for both SQLite and SQL Server using `umbracoDbDSN` connection string. Shares the Umbraco database.
- **Swagger/OpenAPI** — registers a dedicated API document (`umbracocommunityblockrestrictions`) with a security filter extending `BackOfficeSecurityRequirementsOperationFilterBase` to enable backoffice token auth.
- **Hosted service** — `BlockRestrictionMigrationHostedService` applies pending EF Core migrations on app start.
- **Scoped services** — `BlockRestrictionStore` (data access) and `BlockRestrictionService` (business logic).

### Database (`Infrastructure/`)

A single table `BlockRestrictionRules` with the schema:

| Column | Type | Notes |
|---|---|---|
| `Id` | `int` | Primary key, auto-increment |
| `DocumentTypeKey` | `Guid` | Unique index — one rule per document type |
| `AllowedBlockAliasesJson` | `string` | JSON array of element type aliases, e.g. `["heroBlock","gridBlock"]` |
| `CreatedAt` | `DateTime` | UTC timestamp |
| `UpdatedAt` | `DateTime` | UTC timestamp |

The table is created automatically via EF Core migrations on startup. Both SQLite and SQL Server migrations are included.

**Caching**: `BlockRestrictionStore` uses `IMemoryCache` with a 30-minute sliding expiration per document type key. Cache is invalidated on upsert and delete operations.

### Service (`BlockRestrictionService.cs`)

Key methods:

#### `ResolveAllowedBlocksForNodeAsync(Guid nodeKey)`

The core restriction resolution logic used by the property editors at content editing time:

1. Checks `IMemoryCache` for a previously resolved result (keyed by node key + rule version)
2. On cache miss, looks up the content node by key via `IContentService`
3. Gets the node's content type and checks for a restriction rule
4. If no rule found, walks up the content tree via `ParentId` (setting `InheritedFromAncestor = true`)
5. At each ancestor, checks the document type key against stored rules
6. When a rule is found, resolves the allowed aliases to content element type GUIDs via a single batched `IContentTypeService.GetAll()` call with dictionary lookup
7. Returns `AllowedBlocksResponse` with `HasRestrictions = true`, the allowed keys, and inheritance info
8. If no rule found at any level, returns `HasRestrictions = false` (fail-open)
9. Caches the result with a 60-second absolute expiration

**Cache invalidation**: A static `_ruleVersion` counter is included in cache keys. When any rule is saved or deleted, the counter is incremented via `Interlocked.Increment`, which naturally invalidates all cached resolved results without needing prefix-based cache eviction. Old entries expire via TTL.

#### `ResolveContentElementTypeKeys(List<string> aliases)`

Resolves element type aliases to GUIDs using a single `IContentTypeService.GetAll()` call, filtering to matching element types and building a dictionary for O(1) lookup per alias. Logs a warning for any alias that doesn't resolve.

#### `GetBlockDataTypes()`

Returns all data types using the restricted property editor UIs (`Block Grid (Restricted)` / `Block List (Restricted)`), filtering by `IDataType.EditorUiAlias`. For each data type, parses the `blocks` configuration array to extract `contentElementTypeKey` GUIDs. Used by the workspace view's "Filter by data type" dropdown.

#### `GetAllElementTypes()`

Returns all element types (content types where `IsElement = true`) with key, alias, name, and icon. Used by the workspace view to populate the block type checklist.

### API (`Controllers/`)

Base URL: `/umbraco/umbracocommunityblockrestrictions/api/v1`

Secured with `[Authorize(Policy = AuthorizationPolicies.SectionAccessContent)]`.

| Method | Route | Response cache | Purpose |
|---|---|---|---|
| `GET` | `allowed-blocks/{nodeKey:guid}` | 60s (client) | Resolve effective restrictions for a content node (walks up tree). Returns 404 if node not found. |
| `GET` | `rules/{docTypeKey:guid}` | — | Get restriction rule for a specific document type. Returns empty body if no rule exists. |
| `PUT` | `rules/{docTypeKey:guid}` | — | Create or update a restriction rule. Body: `{ "allowedBlockAliases": ["alias1", "alias2"] }` |
| `DELETE` | `rules/{docTypeKey:guid}` | — | Delete a restriction rule (returns to inheritance). 404 if no rule existed. |
| `GET` | `element-types` | 300s (client) | List all element types (for the workspace view checklist). |
| `GET` | `block-data-types` | 300s (client) | List restricted block data types with their configured content element type keys. |

Read-only endpoints use `[ResponseCache(Location = ResponseCacheLocation.Client)]` to set `Cache-Control: private, max-age=N` headers, allowing the browser to serve cached responses for the same backoffice user without re-fetching. Write endpoints (`PUT`, `DELETE`) have no response caching.

## Frontend

### Build

The frontend is built with Vite in library mode. Entry point: `src/bundle.manifests.ts`. All `@umbraco-cms/backoffice` and `lit` imports are externalised (resolved at runtime by the Umbraco backoffice shell).

```bash
cd src/UmbracoCommunity.BlockRestrictions/Client
npm ci
npm run build    # tsc && vite build
npm run watch    # tsc && vite build --watch
```

Output: `wwwroot/App_Plugins/UmbracoCommunityBlockRestrictions/`

### API client (`api/client.ts`)

A lightweight authenticated fetch wrapper. Auth is configured per-session by consuming `UMB_AUTH_CONTEXT` from the Umbraco backoffice and calling `setAuthConfig()` with the token function, base URL, and credentials from `getOpenApiConfiguration()`.

All API functions return typed responses and throw on non-OK status codes (except explicit 404 handling).

### Extension manifests

#### Workspace view (`workspace-views/manifest.ts`)

Registers a **Blocks** tab on the Document Type workspace (`Umb.Workspace.DocumentType`). Appears alongside the native Design, Composition, etc. tabs.

#### Property editors (`property-editors/manifest.ts`)

Registers two property editor UIs:

| Alias | Schema | Element |
|---|---|---|
| `UmbracoCommunity.PropertyEditorUi.BlockGridRestricted` | `Umbraco.BlockGrid` | `block-grid-restricted` |
| `UmbracoCommunity.PropertyEditorUi.BlockListRestricted` | `Umbraco.BlockList` | `block-list-restricted` |

Both use the same underlying Umbraco property editor schema as the native editors, so they share configuration, storage format, and rendering.

Each editor also registers supporting extension manifests required by the native block editors:

- **`propertyContext` (clipboard)** — enables clipboard context for copy/paste
- **`propertyContext` (sortMode)** — enables sort mode context for drag/drop reordering
- **`propertyAction` (copyToClipboard)** — copy action (conditional on property having a value)
- **`propertyAction` (pasteFromClipboard)** — paste action (Block List only, conditional on writable property)

These are necessary because the native block editors' contexts are filtered by `forPropertyEditorUis` — without registering our own aliases, clipboard and sort mode contexts wouldn't load, and the "add content" modal routing would fail silently.

The `meta.settings` for each editor mirror the native editor's configuration properties (live editing, editor width, grid columns, layout stylesheet, etc.).

### Property editor elements

Both `block-grid-restricted.element.ts` and `block-list-restricted.element.ts` follow the same architecture:

#### Pattern: wrapper element with imperative inner element creation

```
block-grid-restricted (our element, light DOM)
  └── umb-property-editor-ui-block-grid (native element, created imperatively)
```

1. **Light DOM** — `createRenderRoot()` returns `this` to avoid an extra shadow DOM boundary that would interfere with Umbraco context propagation.

2. **Imperative creation** — the inner native element is created via `document.createElement()` after `customElements.whenDefined()` resolves. This ensures the native element class is fully defined before instantiation, avoiding property shadowing issues during custom element upgrade.

3. **Auth context** — consumes `UMB_AUTH_CONTEXT` to configure the API client. Guards against `undefined` callback (context unprovided) to prevent clobbering the auth token stored by the module-level singleton.

4. **Entity context** — consumes `UMB_ENTITY_CONTEXT` to get the current content node's unique key.

5. **Restriction lifecycle**:
   - On load, creates the inner element immediately with the full (unmodified) config so the editor is always functional
   - In parallel, fetches `allowed-blocks/{nodeKey}` from the API
   - When restrictions arrive, computes the effective config and **recreates the inner element** so the block manager initialises fresh with the restricted config
   - Recreation is necessary because the native block manager caches block types from its first config and doesn't re-read on subsequent property updates

6. **Block Grid restriction mechanism** — keeps ALL block type definitions in the config (so existing blocks render correctly) but sets `allowAtRoot: false` and `allowInAreas: false` on non-allowed types. The native block grid entries context uses these flags to filter the "add content" picker.

7. **Block List restriction mechanism** — filters non-allowed block types from the `blocks` config array entirely. Block List has no `allowAtRoot`/`allowInAreas` mechanism, so this is the only option.

8. **Value forwarding** — listens for `property-value-change` events from the inner element and re-dispatches them with `bubbles: true, composed: true` so the Umbraco property system receives the updates.

9. **Restriction info banner** — when restrictions are active, renders an accessible status banner (`role="status"`) above the block editor showing "Block types are restricted" with optional inheritance source. The decorative filter icon uses `aria-hidden="true"`.

### Workspace view element (`block-restrictions.element.ts`)

The configuration UI rendered on the **Blocks** tab of each document type. Features:

- **Restrictions toggle** — enable/disable restrictions for this document type. When off, shows messaging about inheritance.
- **Block type checklist** — scrollable, keyboard-focusable list (`tabindex="0"`, `aria-label="Block types"`) of all element types with checkbox selection, icons, names, and aliases. Decorative icons use `aria-hidden="true"`.
- **Live selection count** — "X of Y block types selected" uses `role="status"` and `aria-live="polite"` so screen readers announce changes when filters or checkboxes are updated.
- **Filter by data type dropdown** — shows only restricted Block Grid/List data types. Selecting one narrows the checklist to only show element types configured on that data type.
- **Text search** — filters the checklist by name or alias.
- **Select/Deselect all** — scoped to the current filter (data type + text search).
- **Visibility toggles** — "Show settings types" and "Show composition types" to show/hide element types prefixed with "settings" or "composition" (hidden by default).
- **Save** — persists the rule via `PUT /rules/{docTypeKey}` or removes it via `DELETE /rules/{docTypeKey}` when toggled off.

## Performance

### Two-tier caching

Restriction resolution uses two layers of caching to minimise database and service calls:

1. **Store-level cache** (`BlockRestrictionStore`) — individual rules are cached in `IMemoryCache` with a 30-minute sliding expiration per document type key. Invalidated on upsert/delete.

2. **Service-level cache** (`BlockRestrictionService`) — fully resolved `AllowedBlocksResponse` objects are cached with a 60-second absolute expiration per content node key. Cache keys include a static rule version counter, so any rule change (save or delete) instantly invalidates all resolved results via `Interlocked.Increment`.

This means a page with multiple restricted block editors makes the full tree walk only once per minute, and subsequent property editors on the same page serve from cache.

### Batched alias resolution

`ResolveContentElementTypeKeys` loads all element types in a single `IContentTypeService.GetAll()` call and builds a dictionary for O(1) lookup per alias. This replaces the previous N individual `Get(alias)` calls, reducing service calls from N to 1 regardless of how many aliases a rule contains.

### Browser response caching

Read-only endpoints set `Cache-Control: private, max-age=N` via `[ResponseCache]`:

- `allowed-blocks`: 60 seconds — prevents re-fetching when navigating back to the same content node
- `element-types` and `block-data-types`: 300 seconds — these change infrequently (only when document types or data types are added/removed)

Write endpoints (`PUT /rules`, `DELETE /rules`) have no response caching.

## Accessibility

The package targets **WCAG 2.1 Level AA** to align with the main site's accessibility standards.

### Property editor info banners

- The "Block types are restricted" banner uses `role="status"` so screen readers announce it when the editor loads
- The decorative filter icon uses `aria-hidden="true"` to prevent screen readers from announcing it, following the project's icon accessibility convention

### Workspace view

- **Keyboard-scrollable block list** — the scrollable checklist has `tabindex="0"` and `aria-label="Block types"` so keyboard users can focus and scroll it (WCAG 2.1.1)
- **Live region for selection count** — the "X of Y block types selected" counter uses `role="status"` and `aria-live="polite"` so screen readers announce changes when filters or checkboxes are updated (WCAG 4.1.3)
- **Decorative icons hidden** — all `<umb-icon>` elements in the block type checklist use `aria-hidden="true"` (WCAG 1.1.1)
- **Form controls labelled** — all inputs, selects, toggles, and buttons have explicit `label` attributes via UUI component properties

## Key design decisions

### Storing aliases vs keys

Restriction rules store element type **aliases** (not GUIDs/keys). This makes rules human-readable in the database and portable across environments where GUIDs may differ. The service resolves aliases to keys at query time via `IContentTypeService`.

### Content tree inheritance (not document type hierarchy)

Restrictions walk up the **content tree** (parent nodes), not the document type inheritance chain. This means a "Blog Post" document type can have different restrictions depending on where in the content tree the blog post lives. This provides more flexibility than document-type-level inheritance.

### Fail-open design

If the restriction API is unavailable (auth failure, network error, etc.), the property editors fall back to showing all blocks. This prevents a misconfigured restriction from locking editors out of content editing.

### Recreate-on-restriction pattern

The native block grid/list managers cache their block type configuration on first initialisation and don't respond to subsequent config property updates. Rather than trying to force the manager to re-read config (which would require reaching into internal APIs), the restricted property editors recreate the inner element when restrictions load. This causes a brief visual refresh but is reliable across all timing scenarios.

## Dependencies

**Backend**: Umbraco CMS 17+ (uses `IContentService`, `IContentTypeService`, `IDataTypeService`, `BackOfficeSecurityRequirementsOperationFilterBase`), Entity Framework Core, `IMemoryCache`.

**Frontend**: `@umbraco-cms/backoffice` (context API, element mixin, auth, entity, notification, property editor types), Lit 3.x, Vite 7.x.

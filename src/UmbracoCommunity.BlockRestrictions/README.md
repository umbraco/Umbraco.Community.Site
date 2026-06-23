# UmbracoCommunity.BlockRestrictions

A Razor Class Library that restricts which block types are available per document type in Umbraco, with content tree inheritance.

## Overview

By default, Umbraco shows all configured block types on every Block Grid or Block List property editor. This package adds per-document-type restrictions so that content editors only see the blocks relevant to the page they're editing.

Rules are inherited through the content tree: a restriction set on a "Blog" document type applies to all blog posts and their children, unless overridden at a lower level.

## Key Concepts

- **Fail-open design** — if no restriction rule exists for a document type (or any of its ancestors in the content tree), all blocks are allowed (existing Umbraco behaviour)
- **Content tree inheritance** — rules are resolved by walking up from the current content node through its parent nodes, not the document type hierarchy
- **Dual persistence** — rules are stored in both the database (for runtime) and JSON files in `umbraco/BlockRestrictions/` (for version control portability)
- **Zip export/import** — rules can be downloaded as a zip bundle and uploaded to another environment, useful for cloud-hosted sites without direct filesystem access

## Using the Backoffice UI

### Configuring Restrictions (Document Type Workspace)

1. Go to **Settings > Document Types** and open any document type
2. Click the **Blocks** tab (added by this package)
3. Toggle **Enable restrictions** on
4. Select which block element types are allowed for this document type
5. Optionally filter by a specific data type using the **Filter by data type** dropdown
6. Click **Update** to save

### Block Restrictions Dashboard (Settings > Advanced)

Navigate to **Settings > Advanced > Block Restrictions** in the backoffice sidebar. This section has two areas:

#### File Import (top box)

Compare JSON rule files on disk (`umbraco/BlockRestrictions/`) against the database:

1. Click **Load Preview** to see a categorised diff:
   - **New Rules** — files with no corresponding DB rule (will be added)
   - **Updated Rules** — files that differ from the DB (will be updated)
   - **Orphaned Rules** — DB rules with no corresponding file (will be deleted, or you can save them to disk first)
   - **Unchanged** — files that match the DB (collapsible)
   - **Unknown Aliases** — files referencing document types that don't exist (skipped)
2. Click **Apply All Changes** to sync the database with the files

#### Export & Import (bottom box)

- **Export from Database** — downloads all current DB rules as a zip of JSON files
- **Export from Disk** — downloads all JSON files currently in `umbraco/BlockRestrictions/` as a zip
- **Upload ZIP** — uploads a zip containing JSON rule files and writes them to `umbraco/BlockRestrictions/`. After uploading, the preview automatically refreshes so you can review and apply the changes

### Using Restricted Property Editors

To use restrictions on a content page, the document type's Block Grid or Block List properties must use the restricted property editor UIs instead of the native ones:

1. Go to **Settings > Data Types**
2. Create a new data type (or edit an existing one)
3. Choose **Block Grid (Restricted)** or **Block List (Restricted)** as the property editor
4. Configure blocks as normal — the restricted editor uses the same schema and configuration as the native editor

When a content editor opens a page using this data type, only the blocks allowed by the restriction rule (resolved via content tree inheritance) will be shown.

## Architecture

### Backend

```
BlockRestrictionComposer.cs          — Registers all services, DbContext, Swagger docs
BlockRestrictionService.cs           — Core business logic (tree walk, caching, file sync, zip export/import)
Controllers/
  BlockRestrictionApiControllerBase.cs — Base controller (routing, auth, versioning)
  BlockRestrictionApiController.cs     — API endpoints
Infrastructure/
  BlockRestrictionDbContext.cs         — EF Core DbContext
  BlockRestrictionEntity.cs            — Database entity
  BlockRestrictionStore.cs             — Data access layer with caching
  BlockRestrictionFileService.cs       — JSON file read/write/delete
  BlockRestrictionMigrationNotificationHandler.cs — Auto-migration after Umbraco starts
Models/
  AllowedBlocksResponse.cs             — Resolution result DTO
  BlockRestrictionFileModel.cs         — JSON file format ({alias}.json)
  FileImportModels.cs                  — Import preview/apply/upload DTOs
  ...
Migrations/                            — EF Core migrations
```

### Frontend (Client/)

```
src/
  api/
    client.ts                          — Typed API client with Umbraco auth
    client.test.ts                     — API client tests
  dashboards/
    manifest.ts                        — Dashboard manifest (Settings > Advanced menu item)
    file-import-dashboard.element.ts   — Import/export UI
  property-editors/
    manifest.ts                        — Property editor manifests + clipboard translators
    block-grid-restricted/             — Block Grid (Restricted) property editor
    block-list-restricted/             — Block List (Restricted) property editor
    translators/                       — Clipboard copy/paste translators
  workspace-views/
    manifest.ts                        — Workspace view manifest (Blocks tab on Document Types)
    block-restrictions/                — Restriction configuration UI
  bundle.manifests.ts                  — Aggregates all manifests for Umbraco extension discovery
```

### API Endpoints

Base URL: `/umbraco/umbracocommunityblockrestrictions/api/v1`

All endpoints require backoffice authentication (Content section access).

| Method | Path | Description |
|--------|------|-------------|
| GET | `allowed-blocks/{nodeKey}` | Resolve effective restrictions for a content node |
| GET | `rules/{docTypeKey}` | Get restriction rule for a document type |
| PUT | `rules/{docTypeKey}` | Create/update a restriction rule |
| DELETE | `rules/{docTypeKey}` | Delete a restriction rule |
| GET | `element-types` | List all element types |
| GET | `block-data-types` | List restricted block data types |
| GET | `file-import/preview` | Preview file-to-DB diff |
| POST | `file-import/apply` | Apply file import to DB |
| POST | `file-import/export-rule/{docTypeKey}` | Export single DB rule to disk |
| GET | `file-import/export-db` | Download all DB rules as zip |
| GET | `file-import/export-files` | Download all disk files as zip |
| POST | `file-import/upload` | Upload zip of rule files to disk |

### Rule File Format

Each rule is stored as `umbraco/BlockRestrictions/{documentTypeAlias}.json`:

```json
{
  "DocumentTypeAlias": "blogPost",
  "AllowedBlocks": [
    "ctaBlock",
    "heroBlock",
    "richTextBlock"
  ]
}
```

Aliases are sorted alphabetically for deterministic git diffs.

## Development

### Building

```bash
# Backend
cd src/UmbracoCommunity.BlockRestrictions
dotnet build

# Frontend
cd src/UmbracoCommunity.BlockRestrictions/Client
npm run build

# Watch mode (frontend)
npm run watch
```

Built frontend assets go to `wwwroot/App_Plugins/UmbracoCommunityBlockRestrictions/`.

### Testing

```bash
cd src/UmbracoCommunity.BlockRestrictions/Client
npm run test              # Run tests
npm run test:coverage     # Run with coverage
```

Tests use Vitest and are colocated with source files (`.test.ts` suffix).

### Database

- Uses EF Core with SQLite (dev) / SQL Server (production) via `IDbContextFactory`
- Migrations run automatically via `BlockRestrictionMigrationNotificationHandler` (on `UmbracoApplicationStartedNotification`)
- The database provider is determined by the Umbraco connection string configuration

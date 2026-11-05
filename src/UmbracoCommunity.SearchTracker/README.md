# UmbracoCommunity.SearchTracker

Logs what people type into site search, per tenant, and shows it in a **Search Terms** dashboard in the backoffice Content section.

## What gets recorded

`SearchPageController` calls `ISearchTermRecorder.Record(siteKey, query, resultCount, HttpContext)` after a search on page 1 of the results. The site key is the request's content root, so Community and Codegarden are kept apart.

The recorder skips the search when:

- the user agent is empty or looks like a bot, crawler, link previewer or HTTP library
- the same client searched the same term on the same site within `DuplicateWindow` (default 30 minutes)
- the client has already logged `MaxSearchesPerClientPerHour` searches (default 60)

The client address is only used as an in-memory key for those checks. It is never written anywhere.

Terms are normalised before storage: Unicode NFKC, lower-cased, whitespace collapsed, capped at `MaxTermLength` (default 100). Email addresses become `[email]`, and digit runs of 7+ digits (phone numbers, IDs) become `[number]`.

## Storage

One table, `SearchTrackerTermDaily`: one row per site, UTC day and term, holding the number of searches and how many of those returned no results. Events are queued in memory and written in batches by `SearchEventWriterService`, so search requests never wait on the database. If the queue is full, events are dropped.

`SearchTrackerRetentionService` deletes rows older than `RetentionDays` (default 365). Editors can also delete a term from the dashboard, which removes it for every date.

Migrations run on `UmbracoApplicationStartedNotification`, like the other packages in this repo.

## Dashboard

Content section → **Search Terms**:

- site and period filters (7 / 30 / 90 / 365 days)
- totals: searches, different terms, searches with no results
- searches per day, with the no-result share highlighted
- a term table sortable by searches, no-result searches, last searched or A–Z, with a per-term trend and delete

Users with content start nodes only see the sites those start nodes sit under.

API base: `/umbraco/searchtracker/api/v1` (`sites`, `summary`, `terms`, `terms/trend`, `DELETE terms`), Content section access.

## Configuration

All optional, under `SearchTracker` in `appsettings.json`:

```json
"SearchTracker": {
  "Enabled": true,
  "RetentionDays": 365,
  "MaxTermLength": 100,
  "MaxSearchesPerClientPerHour": 60,
  "DuplicateWindow": "00:30:00"
}
```

## Building the client

```bash
cd src/UmbracoCommunity.SearchTracker/Client
npm ci
npm run build
```

Output goes to `wwwroot/App_Plugins/UmbracoCommunitySearchTracker/` (gitignored). It's wired into `build.mjs` and `.github/workflows/cloud-artifact.yml`.

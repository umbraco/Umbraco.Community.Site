# Discord Announcements Dashboard Design

**Date:** 2026-07-09
**Goal:** A backoffice dashboard giving editors visibility and control over the Discord blog-post announcement pipeline designed in [2026-07-09-discord-blog-announcements-design.md](2026-07-09-discord-blog-announcements-design.md): what's been posted, when, what was skipped, and manual repost/post actions.

Companion to the announcement pipeline — depends on its Phase 1 tracking table (`AnnouncedBlogPost`) and Phase 3 delivery path.

## Pattern to copy

`Umbraco.Community.NotFoundTracker` is the in-repo template: a Razor Class Library with a Management API controller secured by backoffice auth, a `Client/` Vite project (library mode, externalising `@umbraco/*`) emitting to `App_Plugins/<Name>/`, a dashboard element with tab elements, modals with tokens, and a typed fetch API client. Mirror all of it.

The tracking table, detection service, and dashboard all live together with the pipeline. They have since been extracted into their own Razor Class Library, `src/UmbracoCommunity.BlogAnnouncements/` (backend + `Client/`), mirroring the `Umbraco.Community.NotFoundTracker` layout.

## Dashboard placement

Registered as a dashboard in the **Content** section (editors, not developers, are the audience), labelled "Blog Announcements". Weight it below the default content dashboard.

## Tabs

### 1. Posts (default)

Paged, sortable table over `AnnouncedBlogPost` joined with the cached Sphere data (title/author/avatar come from the tracking row itself so history survives posts falling out of the 60-post cache window).

| Column | Notes |
|---|---|
| Post | Title (external link to the post), author name + avatar |
| Published | `PublishedAt` |
| Status | Badge: `Announced` (green) / `Pending` (amber) / `SkippedTooOld` (grey) / `Failed` (red — see below) |
| Announced | `AnnouncedUtc`, relative + absolute on hover |
| Actions | Row menu: **Repost**, **Post now** (for `Pending`/`SkippedTooOld`), **View details** |

Filters: status dropdown, free-text search on title/author, date range. Default view: last 30 days, all statuses.

**Repost** — confirm dialog ("This will post to Discord again — sure?"), then re-fires the delivery path for that post and appends to the announcement history (see Details modal). Guard: disabled while a delivery for the same post is in flight.

**Post now** — same delivery path, but for posts that were never announced (`SkippedTooOld` from the initial seeding, or `Pending` behind the per-cycle cap). This is also the UI for working through the Phase 4 backfill list: seed those rows as `SkippedTooOld`, then post the ones worth announcing by hand, at human pace.

### 2. Activity / Runs

One row per detection cycle: timestamp, posts fetched, new, announced, skipped, failed, dry-run flag. Gives an at-a-glance answer to "is the pipeline alive?" without reading logs. Requires a small `AnnouncementRun` table (or structured log the controller can query — table is simpler and matches the NotFoundTracker approach).

### 3. Settings (read-only first pass)

Shows effective config: recency window, per-cycle cap, dry-run flag, webhook configured yes/no (never the URL itself), Automate flow linked yes/no. Editing stays in `appsettings` (the nested `CommunityBlogs:Announcements` section; the webhook URL goes in appsettings.Local.json / env as `CommunityBlogs:Announcements:Discord:WebhookUrl`) / Automate for now; the tab exists so the state is visible. A **"Send test message"** button posts a canned embed to the webhook — the first thing you reach for when Discord looks quiet.

## Details modal

Per post: full embed preview (roughly as Discord will render it — title, excerpt, author, cover image), the raw tracking row fields, and an **announcement history** list (every delivery attempt: when, outcome, HTTP status, triggered-by: auto / repost / post-now / backfill). History needs a child table `AnnouncementAttempt` rather than the single `AnnouncedUtc` column — worth adding to the pipeline's Phase 1 schema from the start.

## Schema additions to the pipeline design

- `AnnouncedBlogPost` gains `AuthorName`, `AuthorAvatarUrl`, `Excerpt`, `CoverImageUrl` (denormalised so the dashboard doesn't depend on the transient cache), and a `Failed` status.
- New `AnnouncementAttempt` table: `Id`, `SphereId` (FK), `AttemptedUtc`, `Outcome`, `HttpStatus`, `Trigger` (`Auto`/`Repost`/`PostNow`/`Backfill`).
- New `AnnouncementRun` table: `Id`, `RunUtc`, `Fetched`, `New`, `Announced`, `Skipped`, `Failed`, `DryRun`.

## API endpoints

Management API controller (backoffice auth, `SectionAccessContent` policy — same as BlockRestrictions), base `/umbraco/blogannouncements/api/v1`:

- `GET posts` — paged/filtered tracking rows
- `GET posts/{id}` — details + attempt history
- `POST posts/{id}/announce` — repost / post-now (body: trigger reason)
- `GET runs` — paged run log
- `GET settings` — effective config snapshot
- `POST test-message` — canned embed to the webhook

Repost/post-now must call the same delivery path as the automatic flow (the Automate trigger or `IDiscordAnnouncer` fallback) so formatting can never drift between manual and automatic posts.

## Other ideas (rank by value, build later)

- **Failure notification** — if a run ends with `Failed` deliveries, surface an Umbraco backoffice notification rather than waiting for someone to open the dashboard.
- **Suppress button** — mark a `Pending` post as "don't announce" (spam, wrong language, duplicate that slipped the fingerprint). Needs a `Suppressed` status.
- **Edit-before-post** — let the modal tweak the embed title/excerpt before a manual post. Small field set, big editor goodwill.
- **Stats header** — tiles above the Posts tab: announced this week, pending, failures last 7 days.
- **Multi-destination readiness** — if more destinations arrive (Slack, Bluesky), `AnnouncementAttempt` gets a `Destination` column and the status badge becomes per-destination. Don't build now; don't paint into a corner (keep `Destination` on the attempt table from day one, defaulted to `Discord`).

## Build order

1. Schema additions (land together with pipeline Phase 1 so there's one migration).
2. API controller + typed client.
3. Posts tab + details modal + repost/post-now.
4. Runs tab + test-message button.
5. Settings tab, stats tiles, suppress — as time allows.

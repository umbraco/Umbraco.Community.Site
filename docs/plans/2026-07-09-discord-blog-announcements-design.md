# Discord Blog Announcements Design

**Date:** 2026-07-09
**Goal:** Replace the Zapier RSS→Discord flow for announcing new community blog posts with a reliable pipeline built on the Sphere API, a persistent "announced posts" tracking store, and Umbraco Automate for the Discord delivery step.

## Problem

Today a Zapier setup watches a collection of RSS feeds and posts new items to Discord. It has two chronic failure modes:

1. **Duplicates** — sites hosted on Azure surface the same post under both the `*.azurewebsites.net` domain and the custom domain, so posts appear twice.
2. **Repost floods** — when an author changes their RSS setup, their entire back-catalogue re-appears as "new" and gets re-posted to Discord.

Also, the Sphere feed now includes sources that were never wired into Zapier, so recent posts from those feeds have not been announced at all.

## Approach

The site already ingests Sphere blog posts (`Features/Feeds/CommunityBlogs/`, refreshed every 6 hours by `CommunityBlogsBackgroundService`). We add a persistent tracking table of announced posts, a detection step that diffs each refresh against it, and an Umbraco Automate flow that posts the new items to a Discord webhook. Dedup keys on Sphere's stable post `id` (a GUID, verified against the live API), with a recency window and per-cycle cap as guardrails against floods.

Automate is used for the delivery leg so the Discord webhook URL, message format, and any future destinations are editable flows in the backoffice, not code. If Automate proves immature at launch (final release targeted 2026-07-09), the fallback is a plain `IDiscordAnnouncer` service posting the webhook directly — everything else in this design is unchanged.

## What the Sphere API gives us (verified 2026-07-09)

- `GET /api/v1/blog-posts` — cursor-paginated, newest first, `from`/`to` date filters. **Requires** the bare `psk_` key in the `Authorization` header (401 without).
- Post fields: `id` (GUID — stable dedup key), `type`, `platform`, `title`, `url`, `content` (excerpt), `coverImageUrl`, `publishedAt`, `author { name, profileUrl, avatarUrl, type }`.
- `platform` and `author.type` are not yet mapped in `SphereBlogPostsDtos.cs` — add while in there.
- `publishedAt` is often a bare midnight timestamp (date-only feeds) — recency logic should be day-granular in spirit; same-day ordering has ties.
- `avatarUrl` coverage ~80%. Sources pass through from the author's profile: forum/GitHub/Mastodon/Discord CDNs (stable PNGs), LinkedIn (signed, **expiring** URLs — fine for post-once, never store for re-use), and occasionally an SVG (Discord won't render SVG avatars).

> **See also:** [2026-07-09-discord-announcements-dashboard-design.md](2026-07-09-discord-announcements-dashboard-design.md) — backoffice dashboard over this pipeline. Its schema additions (denormalised post fields, `AnnouncementAttempt` and `AnnouncementRun` tables, `Failed` status) should land in the same Phase 1 migration.

## Phase 1 — Announcement tracking store

EF Core table mirroring the `Umbraco.Community.NotFoundTracker` pattern (DbContext, SQLite/SQL Server switching on `umbracoDbDSN_ProviderName`, migrations assembly, startup migration `IHostedService`, design-time factory). This pipeline now lives in its own Razor Class Library, `src/UmbracoCommunity.BlogAnnouncements/`.

`AnnouncedBlogPost`:

| Column | Notes |
|---|---|
| `SphereId` (PK) | Sphere post GUID |
| `Url`, `Title`, `PublishedAt` | For diagnostics and the fingerprint |
| `Fingerprint` | Normalized author + normalized title + publish date — secondary dup guard (same-post-different-domain) |
| `FirstSeenUtc` | When our refresh first saw it |
| `AnnouncedUtc` (nullable) | Null until Discord delivery confirmed |
| `Status` | `Announced` / `SkippedTooOld` / `Pending` |

## Phase 2 — New-post detection

Hook into the existing refresh: after `CommunityBlogsAggregator` returns fresh data, diff against the table.

- Never seen + published within the recency window (default 7 days) → **announce**.
- Never seen but older than the window → record as `SkippedTooOld`, silently. (Kills the repost-flood failure mode.)
- Already seen by `SphereId` or `Fingerprint` → ignore. (Kills the duplicate failure mode.)
- Cap announcements per refresh cycle (default 5); the rest stay `Pending` and drain on subsequent cycles.
- Mark `Announced` only after the delivery step confirms success, so a failed Discord call retries next cycle instead of being lost.
- Dry-run mode (config flag, `CommunityBlogs:Announcements:DryRun`): log what would be posted, post nothing — used during cutover. All announcement settings live in the nested `CommunityBlogs:Announcements` section (`RecencyWindowDays`, `MaxAnnouncementsPerCycle`, `DryRun`, `Discord:WebhookUrl`).

## Phase 3 — Umbraco Automate flow

- Install Umbraco Automate (verify licensing — open source on GitHub but listed under paid add-ons).
- Custom C# trigger — "Community blog post detected" — fired from the detection step, payload: `title`, `url`, `excerpt`, `authorName`, `authorProfileUrl`, `avatarUrl`, `coverImageUrl`, `publishedAt`.
- Backoffice flow: trigger → HTTP request action → Discord webhook.
- **Spike first**: the Automate extending docs confirm attribute-based custom triggers (`[Trigger]` + base class, auto-discovered) but the concrete API for raising a trigger from server code needs verifying against the released package.

## Discord delivery details

- **Mechanism**: a Discord incoming webhook (channel → Integrations → Webhooks). Create a *fresh* one for this flow (distinct from Zapier's) so the parallel-run period is distinguishable and killing the Zap can't affect us. URL is a secret — configured as `CommunityBlogs:Announcements:Discord:WebhookUrl` in appsettings.Local.json / env (or the Automate connection/config), never the repo.
- **Set the webhook's own avatar to the community logo** — then null and SVG `avatarUrl`s both degrade to that logo with zero code.
- Payload per post:
  - `avatar_url` → Sphere `author.avatarUrl` (skip when it ends in `.svg`); no third-party favicon services.
  - `embeds[].author` → author name + avatar, linked to `profileUrl`.
  - `embeds[].title` + `url` → clickable post title.
  - `embeds[].description` → excerpt.
  - `embeds[].image` → `coverImageUrl` when present.
  - `embeds[].timestamp` → `publishedAt`.

## Phase 4 — 30-day backfill / gap analysis

One-off, before or during cutover:

1. Pull the last ~30 days from Sphere (`from`/`to` params).
2. Gather what actually reached Discord: Zapier run history if the plan's retention allows, otherwise Discord channel history (bot token or manual export — the webhook itself cannot read).
3. Diff. Seed the tracking table with everything already posted (`Announced`, backdated).
4. Review the remainder — posts from the newer feeds never announced — and drip-post them through the same Automate flow at a gentle pace.

## Phase 5 — Cutover

1. Ship Phases 1–3 with dry-run on; run alongside Zapier for 1–2 weeks and compare logs against the channel.
2. Seed via Phase 4, flip dry-run off, drip the backlog.
3. Turn the Zap off.

## Open questions

- Automate licensing terms for this site.
- Automate's programmatic trigger-firing API (spike).
- Zapier run-history retention on the current plan (fallback: Discord channel history).

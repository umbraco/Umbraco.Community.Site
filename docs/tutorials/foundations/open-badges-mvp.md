---
tags: [open-badges, members, mvp, png, hmac]
---

# How to issue verifiable Open Badges 2.0 without a database table

Current Umbraco MVPs can download an "Umbraco MVP {year}" badge from their account page and upload it to their Sessionize speaker profile, where it shows as a verified badge. Sessionize only accepts Open Badges 2.0 images: a PNG with the badge assertion baked into it, which a verifier checks against a URL we host. This tutorial walks through how the site issues those badges with no new storage: every document is rebuilt on request from the MVP data we already have, and the assertion id is a signed token that resolves back to the member and year.

## Why you might want this

An image of a badge proves nothing: anyone can copy it. An Open Badge carries an *assertion* (who got which badge, when, and until when) and points at a copy of that assertion hosted by the issuer. A verifier fetches the hosted copy, checks it came from the issuer's site, and shows the badge as valid, expired or revoked. If you already know who has earned something (here: the MVP list and the profile data), you can issue badges for it without building a badge database.

## What we're building

Five endpoints, all in [`OpenBadgesController`](../../../src/UmbracoCommunity.Web/Features/OpenBadges/Controllers/OpenBadgesController.cs), routed in [`UseApplicationEndpoints`](../../../src/UmbracoCommunity.Web/Extensions/UmbracoEndpointBuilderContextExtensions.cs) next to `robots.txt` and the Web Bot Auth directory (paths in [`OpenBadgesRoutes`](../../../src/UmbracoCommunity.Web/Features/OpenBadges/OpenBadgesRoutes.cs)):

| Path | Who | What |
|------|-----|------|
| `GET /badges/issuer.json` | anyone | The Issuer profile |
| `GET /badges/mvp/{year}.json` | anyone | The BadgeClass "Umbraco MVP {year}" |
| `GET /badges/mvp/{year}.png` | anyone | The badge artwork (the BadgeClass `image`) |
| `GET /badges/assertions/{id}.json` | anyone | A member's hosted Assertion |
| `GET /account/badges/umbraco-mvp-{year}.png` | the signed-in member | Their badge PNG with the assertion baked in |

The JSON documents are served as `application/ld+json` (plain `application/json` when that is all the client accepts, as the spec asks), with `Access-Control-Allow-Origin: *` and the `ExternalApi` output cache policy (varying on `Accept`). The download is `Cache-Control: no-store` and an attachment named `umbraco-mvp-{year}.png`.

Every `id` is the absolute URL the document is served from, built from the request's scheme and host. That matters: hosted verification only accepts an assertion served from the same origin as its issuer.

## Walkthrough

### Who gets a badge

[`MvpBadgeEligibility`](../../../src/UmbracoCommunity.Web/Features/OpenBadges/Infrastructure/MvpBadgeEligibility.cs) uses `ProfileMvp.Resolve`, the same rule the public profile uses for its MVP pill: the MVP years the external content platform reports for the GitHub handle (from `ProfileActivityService`'s per-handle cache), otherwise the years in `Features/Mvp/Data/mvp.json`, matched on handle without regard to case. A member is a *current* MVP when their newest year is the newest year known anywhere.

- Only a **current** MVP can download a badge, and only for their current year.
- The hosted assertion of a **past** year still resolves, so a badge downloaded last year keeps verifying, but its `expires` is in the past and verifiers show it as expired.
- An id whose handle was not an MVP in that year returns 404.

### The assertion id

[`MvpBadgeTokenService`](../../../src/UmbracoCommunity.Web/Features/OpenBadges/Infrastructure/MvpBadgeTokenService.cs) makes the id as `base64url(utf8("{handle}:{year}") + mac)`, where `mac` is the first 16 bytes of an HMAC-SHA256 (with `OpenBadges:SigningKey`) over the same text. So the id:

- is stable: the same member and year always give the same id, so a re-download bakes the same URL;
- resolves back to handle and year without a lookup table;
- can't be made up for someone else without the key (only the canonical base64url spelling is accepted, and the MAC is compared in constant time).

A bad id is rejected before anything is looked up.

### The documents

[`OpenBadgeDocuments`](../../../src/UmbracoCommunity.Web/Features/OpenBadges/Infrastructure/OpenBadgeDocuments.cs) builds the three JSON-LD documents with `@context: https://w3id.org/openbadges/v2`:

- **Issuer**: `id`, `type: Issuer`, `name` (`OpenBadges:IssuerName`), `url` (`OpenBadges:IssuerUrl`, or the site itself), and `email` only when `OpenBadges:IssuerEmail` is set.
- **BadgeClass**: `id`, `name`, `description`, `image`, `criteria` (a narrative, plus `OpenBadges:CriteriaUrl` as its `id` when set), `issuer`, `tags`.
- **Assertion**: `id`, `recipient` (`type: url`, `hashed: false`, `identity` = the member's public profile URL), `badge`, `verification: { type: HostedBadge }`, `issuedOn`, `expires`.

The recipient is the profile page URL rather than a hashed email: it is public anyway, and it lets anyone looking at the badge find the person. [`CommunityProfileRecipientResolver`](../../../src/UmbracoCommunity.Web/Features/OpenBadges/Infrastructure/CommunityProfileRecipientResolver.cs) finds the `CommunityProfilePage` of the tenant whose domain matches the request (as `RobotsController` does) and appends the handle. A site without a profile page issues no badges.

### Dates and expiry

Nothing is stored, so the dates are derived from the year and never change between requests:

- The MVP year runs **June to June**. `issuedOn` is the start of it: `{year}-06-01T00:00:00Z`.
- `expires` is the end of it, `{year+1}-06-01T00:00:00Z`, moved on by `OpenBadges:ExpiryGraceMonths` months (default 1, so a 2026 badge expires on `2027-07-01T00:00:00Z`). The buffer stops badges lapsing in the weeks between the year turning over and the next MVPs being announced; set 0 to expire exactly on 1 June.

### Baking the PNG

[`PngBadgeBaker`](../../../src/UmbracoCommunity.Web/Features/OpenBadges/Infrastructure/PngBadgeBaker.cs) follows the Open Badges 2.0 Baking Specification by hand (no extra package): it inserts one `iTXt` chunk with the keyword `openbadges`, compression flag and method 0, empty language tag and translated keyword, straight before the first `IDAT`, with the CRC-32 computed over chunk type and data. Any existing `openbadges` chunk is dropped, since only one is allowed.

The baked text is the assertion JSON, as the 2.0 Baking Specification describes (a bare URL is the 1.x "legacy" style). Its `id` is the hosted URL, which a verifier re-fetches. If a consumer turns out to want the URL instead, set `OpenBadges:BakeAssertionUrl` to `true`.

### The artwork

[`MvpBadgeArtwork`](../../../src/UmbracoCommunity.Web/Features/OpenBadges/Infrastructure/MvpBadgeArtwork.cs) reads `Features/OpenBadges/Assets/mvp-{year}.png`, shipped as embedded resources (`<EmbeddedResource Include="Features\OpenBadges\Assets\mvp-*.png" />` in `UmbracoCommunity.Web.csproj`). It resizes the image once to `OpenBadges:ImageSize` (default 400×400, Sessionize's recommendation) with ImageSharp, which comes with Umbraco, and keeps the result in memory. **A year without artwork has no badge**: its BadgeClass and image return 404 and the account page shows no download.

## Configuration

| Key | Default | Notes |
|-----|---------|-------|
| `OpenBadges:SigningKey` | *(empty)* | **Secret**, at least 32 characters. Set it in `appsettings.Local.json` locally and as an environment variable / Cloud secret elsewhere; never commit it. While it's empty (or too short) every badge endpoint returns 404 and the account page shows no badge. Changing it breaks every badge already downloaded. |
| `OpenBadges:IssuerName` | `Umbraco Community` | |
| `OpenBadges:IssuerUrl` | the site | |
| `OpenBadges:IssuerEmail` | `community@umbraco.com` (set in appsettings.json) | Public contact address shown in the issuer profile. |
| `OpenBadges:CriteriaUrl` | *(none)* | A page explaining the MVP programme. |
| `OpenBadges:ExpiryGraceMonths` | `1` | See *Dates and expiry*. |
| `OpenBadges:ImageSize` | `400` | 0 keeps the artwork's own size. |
| `OpenBadges:BakeAssertionUrl` | `false` | Bake the URL instead of the JSON. |

Generate a key with, for example, `openssl rand -base64 48`.

## Adding a new year

1. Put the square PNG artwork at `src/UmbracoCommunity.Web/Features/OpenBadges/Assets/mvp-{year}.png` (the glob in the csproj picks it up).
2. Add the year's MVPs to `mvp.json` (or let the platform report them).
3. Deploy. Members whose newest MVP year is the new year now see the download; last year's badges start showing as expired once their `expires` passes.

## How a member uploads it to Sessionize

On their account page, a current MVP sees "Your Umbraco MVP {year} badge" with a download button. In Sessionize they open their profile, choose Edit, go to Badges and upload the PNG. Sessionize reads the baked assertion and fetches the hosted copy from this site, so the site must be reachable on the same host the badge was downloaded from.

## Trade-offs and known limits

- **No revocation.** Without a table there is no revocation list; removing someone from the MVP data makes their assertion 404, which verifiers treat as invalid.
- **The host is part of the badge.** Ids are built from the request host, so a badge downloaded on a staging host only verifies there. Download production badges from production.
- **Key rotation breaks badges.** The signing key is part of every id; rotating it means members download their badge again.
- **Sessionize's own checks are untested here.** The documents follow the 2.0 spec and the tests check the PNG structure, but whether Sessionize accepts a given file can only be confirmed with a real upload.

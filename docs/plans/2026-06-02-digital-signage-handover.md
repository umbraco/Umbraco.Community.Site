# Digital signage handover (2026-06-02)

Codegarden venue-screen feature, sitting on **`feature/sessionize-configuration-component`**.

## Goal

Big-screen kiosk display showing the current talk + next talk per room, pulling live from Sessionize. Editor configures `ProgramFeatureBlock`(s) inside a Content Page that uses the new "Digital signage page" template; the same blocks can be used on regular pages too.

## State of the world

All work is **uncommitted** on top of `016092b`. Build is clean. Verified working in the browser with real Sessionize data.

### Files added (uncommitted, untracked)

| Path | Purpose |
|---|---|
| `src/UmbracoCommunity.Web/Features/Sessionize/Infrastructure/ProgramSessionResolver.cs` | Scoped service. Three methods for the three config types; uses `Europe/Copenhagen` for "now". Optional `nowOverride` parameter on `ResolveCurrentAsync` for testing. Always returns configured rooms (even with no sessions); "Up next" falls back to next-ever, not just next-today. |
| `src/UmbracoCommunity.Web/Features/Sessionize/Models/RoomSessionStatus.cs` | DTO: RoomId, RoomName, Current, Next. |
| `src/UmbracoCommunity.Web.UI/Views/Partials/Blocks/ProgramFeatureBlock.cshtml` | Block partial. Pattern-matches the BlockListItem config onto CurrentSessionConfig / HighlightedSessionsConfig / SessionsConfig and renders accordingly. Reads `?signage-now=` from the query string for time simulation. |
| `src/UmbracoCommunity.Web.UI/Views/digitalSignagePage.cshtml` | Full HTML doc (`Layout = null`). Renders `BlockContent`. Contains the fixed clock element. |
| `src/UmbracoCommunity.StaticAssets/src/entrypoints/_digital-signage.ts` | Imports CSS, runs the polling refresh (60 s, page-refetch swap of `[data-program-body]`), and runs the clock. Honours `signage-now` for both. |
| `src/UmbracoCommunity.StaticAssets/src/css/pages/digital-signage.css` | Lit-styles. Mostly fluid typography via `clamp(min, vw, max)`. Single-room mode uses `.dc-program-rooms:has(> :only-child)` selectors to fill the viewport with a much larger current-session card. |

### Files modified

- `src/UmbracoCommunity.Web/Features/Sessionize/Configuration/RegisterSessionize.cs` — registers `ProgramSessionResolver` as scoped.
- `src/UmbracoCommunity.Web.UI/wwwroot/css/styles.css` — backoffice preview styles for `.dc-program-feature` and its slots (lighter, matching the file's flat / hardcoded-hex convention).
- Two existing UDA files (`data-type__29dcf3a2…`, `document-type__1e66f4d1…`) — Content Page now allows the new template; the new data type for "Sessions config" / "Highlighted sessions config" picker.

### Files untracked but generated

- `Documentation.generated.cs` (Models Builder output for a different doctype — unrelated, just sitting there).
- `document-type__e93cd0b2…` and `template__0bbe211b…` UDAs for the new "Digital signage page" template + the program-feature doctype graph.

## Test recipes

- **Real time**: visit any Content Page with the "Digital signage page" template selected.
- **Simulated time**: append `?signage-now=2026-05-21T09:25` (any ISO-ish datetime; partial uses `DateTime.TryParse` with invariant culture, `AssumeLocal`). An orange `SIMULATED` chip appears below the clock when active. The polling refresh preserves the query string so the override persists across refreshes.
- **Single vs multi-room layout**: configure one or many rooms in a `CurrentSessionConfig`. The `:has(> :only-child)` selector switches the typography scale up significantly.

## Recent tweaks worth remembering

- Title size has been retuned a couple of times. Currently `clamp(2.5rem, 5vw, 12rem)` for single-room current title (line ~295 in `digital-signage.css`) — the user manually tuned this after I tried 9vw/14rem. Don't overwrite without checking.
- Colours all come through `var(--color-identity-*)` from `root.css`. Single-room hero card: blue background, orange "NOW" pill, yellow time, pink speakers, white title.
- The block partial uses **single-generic** `BlockGridItem<ProgramFeatureBlock>` (not `BlockGridItem<TContent, TSettings>`) because the block grid config doesn't have a Settings type wired. `Model.Settings` is cast via `as SettingsProgramFeatureBlock` so it works with or without settings attached.

## Known TODO / nice-to-haves

- `Europe/Copenhagen` is hardcoded in both `ProgramSessionResolver.ResolveCopenhagenTimeZone()` and the JS (`EVENT_TIMEZONE` in `_digital-signage.ts`). Fine for Codegarden 2026, but should be configurable if reused.
- Polling refresh re-fetches the entire page. Works fine for a dedicated kiosk; if we want to optimise, a `/api/program-feature/{id}/render` endpoint returning just the fragment would cut payload.
- `HighlightedSessionsConfig` and `SessionsConfig` render to a simpler card list — functional but less visually polished than the current-session view.
- No `[OutputCache]` on the digital signage path (deliberate — the page MUST be live for current/next to update). `SessionizeApiClient` still caches the underlying API response for 60 min, so the data is cheap.

## Suggested next actions when picking up

1. Commit the work — it splits naturally into:
   - "Add ProgramSessionResolver and ProgramFeatureBlock partial"
   - "Add digital signage page template, CSS, polling refresh and clock"
   - "Add backoffice preview styles for program feature block"
   - UDA bumps for the new content / template.
2. Decide if the simulated-time mode should ship to production or be gated to dev. Right now it works everywhere — useful, but anyone can pass `?signage-now=` on the live URL.
3. Consider the polish items above (the `is-empty` slot styling when nothing's on isn't great; the single-room "Up next" line could probably do with more breathing room).

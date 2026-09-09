# Community Meet booking — Apps Script

The Google half of [`docs/plans/2026-09-03-community-meet-booking-design.md`](../../docs/plans/2026-09-03-community-meet-booking-design.md).
Runs as `community@umbraco.com`. Creates the Calendar event + Meet, applies the host controls, adds the requester as co-host.

Files:

| File | What |
|---|---|
| `appsscript.json` | Manifest: scopes, Calendar advanced service, V8 runtime |
| `Code.gs` | `doPost` (JSON API for Umbraco), `doGet` + `provisionFromForm` (approver form), `provision_` (the work), test/diagnostic/cleanup helpers |
| `Form.html` | The approver form template |

## Set up (once, signed in as community@)

1. **Calendar.** In Google Calendar, create a secondary calendar "Community Meets" on the account and share it with the community team. (A secondary calendar suppresses continuous meeting chat, but `CONFERENCE_MODE=own-space` rules chat out anyway, so it costs nothing here. If you ever switch to `calendar` mode for the chat, bookings have to move to `primary`.)
2. **Script.** Go to [script.google.com](https://script.google.com) → New project → name it `Community Meet Booking`.
   Either paste the three files in by hand (Project Settings → tick *Show "appsscript.json" manifest file* first), or push with clasp:
   ```bash
   npm i -g @google/clasp
   clasp login                       # as community@
   cd tools/meet-booking-apps-script
   clasp create --type standalone --title "Community Meet Booking"
   clasp push
   ```
   Keep the generated `.clasp.json` out of git (it's in `.gitignore`); the script id is not secret but it's per-deployment.
3. **Script Properties** (Project Settings → Script Properties):

   | Property | Value |
   |---|---|
   | `SHARED_SECRET` | `openssl rand -hex 32`. Same value goes into Umbraco's `MeetBooking:Google:AppsScript:SharedSecret`. |
   | `CALENDAR_ID` | Run `listCalendars()` in the editor and copy the id of "Community Meets". Defaults to `primary` if unset. |
   | `TEST_COHOST_EMAIL` | A Google account *you* control (a personal gmail is ideal — that's what most requesters will have). |

   Two more properties appear on their own: **`LAST_TEST_STATE`** and **`LAST_TEST_CALENDAR`**. They aren't configuration — `testProvision()` writes them so `cleanupTest()` can find and delete the event it made, and `cleanupTest()` removes them again. If you delete a test event by hand they'll linger; run `cleanupTest()` or delete them yourself, either is fine.
   | `APPROVER_DOMAIN` | Leave unset for `umbraco.dk`. Comma-separated to allow more, e.g. `umbraco.dk,umbraco.com`. |
   | `SPACE_CONFIG` | Leave unset to use the defaults in `Code.gs`. Set to a JSON object of the same shape to change host controls without a code change. Defaults include `accessType: 'TRUSTED'` — the invited requester joins directly, anyone else with the link asks to join. Set `accessType` to `'OPEN'` here to let everyone straight in. **Must be a whole config object**, not a bare value: `{"accessType":"OPEN", ...}`. Copy `DEFAULT_SPACE_CONFIG` from `Code.gs` and change what you need — setting it to just `OPEN` fails the booking with a `step: "config"` error. |
   | `COHOST_MODE` | Leave unset (`api`) — the Cloud project is enrolled in the Developer Preview Program as of 2026-09-09, so the script adds the co-host itself. `auto` makes the same call but degrades to manual instructions instead of failing the booking; `manual` never calls the API. See *Co-host modes* below. |

4. **Cloud project + Meet API.** Apps Script gives the project a hidden default Cloud project, which is fine for Calendar but the Meet REST API has to be *enabled*, and the co-host endpoint is Developer Preview (skip 4.4 only if you're running `COHOST_MODE=manual`):
   1. [console.cloud.google.com](https://console.cloud.google.com) → New project (e.g. `community-meet-booking`). If the account can't create projects, that's the one thing to ask IT for.
   2. APIs & Services → Enable **Google Meet REST API** and **Google Calendar API**.
   3. APIs & Services → OAuth consent screen → *Internal*, fill in the app name and contact.
   4. Enrol the project in the [Google Workspace Developer Preview Program](https://developers.google.com/workspace/preview) — [`spaces.members`](https://developers.google.com/workspace/meet/api/guides/meeting-spaces-configuration) is preview-only and is the whole reason we need it. Enrolment is per Cloud project, so a new project has to be enrolled again.
   5. Back in Apps Script: Project Settings → Google Cloud Platform (GCP) Project → *Change project* → paste the project number.
5. **Authorise.** In the editor run `showConfig()`. The consent screen lists the scopes from the manifest; click *Allow*. `showConfig()` should print `calendarSummary: "Community Meets"` and `sharedSecretSet: true`.

## Conference mode: continuous chat vs automated co-hosts

These two cannot both be had, and `CONFERENCE_MODE` picks which. **The trade was decided in favour of automated co-hosts** (2026-09-10):

| | `own-space` (default) | `calendar` |
|---|---|---|
| Who makes the Meet | us, via `spaces.create`, then attached to the event | Calendar, via `conferenceData.createRequest` |
| **Automated co-hosts** | **works** | impossible — `spaces.members` 403s |
| Continuous meeting chat | never appears | works (on the primary calendar) |
| Host controls (`spaces.patch`) | work | work |
| Calendar's Meet settings gear | absent | present |
| `conferenceId` on the event | absent (only `entryPoints`) | present |

Host controls are **not** a casualty of `own-space` — `spaces.patch` never cared who created the space, and a read-back of an own-space booking shows `moderation: ON` and `presentRestriction: HOSTS_ONLY` stored as sent. What `own-space` costs is continuous chat and the *gear*, i.e. the Calendar UI for hand-tweaking. Since the co-host is added automatically, the main thing that gear was for is gone anyway.

**Why co-hosts are gated at all**, established 2026-09-09: `spaces.members` only works on a space the calling app created. Against the space behind a Calendar-minted conference it returns `403 PERMISSION_DENIED` on `Member`, while `spaces.patch` on that *same* space with the *same* token succeeds — so it is the space's origin, not the grant, and no scope fixes it. `checkCohostApi()` passing (on a space the script made) while a real booking failed is what isolated it, and `testAttachOwnSpace()` confirmed attaching our own space makes the co-host call work.

**Why this way round.** `presentRestriction` is `HOSTS_ONLY`, which is only safe if somebody in the room can actually present — and that somebody is the requester, added as co-host automatically. The two settings hold each other up: relax the co-host and hosts-only presenting locks everyone out; relax presenting and the co-host stops mattering.

To go the other way — continuous chat, at the cost of a manual co-host step — set `CONFERENCE_MODE=calendar`, `CALENDAR_ID=primary`, `COHOST_MODE=manual`, and `presentRestriction` to `NO_RESTRICTION` in `SPACE_CONFIG`. No code change. That configuration was live and working for an afternoon, so it is a real option, not a theory.

### Co-host modes

`COHOST_MODE` is `api` by default: the requester is added as co-host and a failure **fails the booking**, rather than shipping a hosts-only meeting that nobody present can share a screen in. `auto` makes the same call but degrades to manual instructions with `ok: true`; `manual` never calls the API; `off` ignores co-hosts entirely and only makes sense with presenting unrestricted. `api` and `auto` require `CONFERENCE_MODE=own-space` and are downgraded to `manual` with a warning otherwise.

External addresses are fine — verified with a personal gmail, which is what most requesters will have. That was the design's biggest open risk and it is closed.

Adding a co-host is idempotent when it does run: the script lists the space's members first and skips the create if the email is already a `COHOST`, so a retry — including a stateless one that re-adopts an existing event — cannot fail on a duplicate.

### Continuous meeting chat: given up, on purpose

Bookings do not get continuous meeting chat, and that is a **decision** rather than a defect: having it requires `CONFERENCE_MODE=calendar`, which is exactly the case where the co-host API refuses to work. An automatic co-host was judged worth more.

There are two independent suppressors, which took five experiments to pin down because each alone looks like a complete explanation:

1. **Attaching our own space.** `CONFERENCE_MODE=own-space` never gets chat, on any calendar.
2. **The secondary calendar.** An event on the "Community Meets" calendar never gets chat — *including one created by hand*, which is what proves it is the calendar and not the API.

The full grid, all measured:

| Conference created by | primary calendar | secondary calendar |
|---|---|---|
| hand, in the Calendar UI | chat ✓ | chat ✗ |
| Calendar, via `createRequest` | **chat ✓** ← what we do | chat ✗ |
| us, attached | chat ✗ | chat ✗ |
| Calendar, via `createRequest`, then moved to secondary | — | chat ✗ |

So chat would need `CONFERENCE_MODE=calendar` **and** `CALENDAR_ID=primary`. There is no `SpaceConfig` field for chat retention, so nothing here can override either; the org-level control is the Admin console (Meet safety settings, per OU/group/user).

One upside of giving chat up: **`CALENDAR_ID` is unconstrained again.** Suppressor (1) already rules chat out, so putting bookings on the shareable "Community Meets" calendar costs nothing and is the nicer choice — the community team can be given the calendar rather than the host account's agenda.

**Moving doesn't help either.** Creating on primary (where chat works) and then moving the event to a secondary calendar loses the chat — so even in `calendar` mode there is no way to have both chat and a separate calendar.

Five wrong causes were written into this repo as fact before the grid above was complete — each one a plausible API-side story, while the variable nobody had changed was the calendar. Prefer "not yet established", and when several explanations in one area all get falsified, suspect the area rather than generating a sixth.

## Test it

All from the script editor, signed in as community@:

1. `showGrantedScopes()` — prints what the current authorisation granted and flags `meetings.space.settings` / `meetings.space.created` as `GRANTED` or `MISSING!`. Editing the manifest does not re-prompt, so a scope added after the last consent is simply absent from the token and surfaces as a 403 on whatever needed it. Check this before theorising about any permission error.
2. `checkCohostApi()` — the preview preflight. Creates a bare Meet space (no event, no invitations), runs list → create → delete against `v2beta/spaces/{space}/members` with `TEST_COHOST_EMAIL`, and logs `PASS` or a diagnosis of the exact failure. Do this first: it's the one part of the flow that depends on the Developer Preview, and it tells you in seconds instead of after a booking half-completes. It leaves the scratch space behind — the API has no delete for a space — which is inert but is why it isn't something to run repeatedly.
3. `testProvision()` — books a 30-minute "TEST — delete me" two days out with `TEST_COHOST_EMAIL` as co-host. Read the log (View → Executions, or the bottom pane). Expected: `ok: true`, `cohostAdded: true`, a Meet link, an event link.
4. Check the mailbox of `TEST_COHOST_EMAIL`: the invite should be there.
5. Open the Meet link **as that account** (incognito). The shield icon should show you as co-host with *Share their screen* off for others, attendance tracking on, and transcription and Gemini notes both off.
6. Open the same link as a third, uninvited account: with the default `accessType: 'TRUSTED'` you should be asked to wait while a host admits you. (Set `accessType` to `'OPEN'` in `SPACE_CONFIG` and everyone walks straight in.)
7. `cleanupTest()` — deletes the event and clears the saved state.

What gets captured is per meeting, from two independent checkboxes, both defaulting **off** — recording or writing down a community meetup nobody asked to have recorded is the worse failure:

| Request flag | Form field | Sets |
|---|---|---|
| `recordMeeting` | *Record the meeting?* | `autoRecordingGeneration` |
| `transcribeMeeting` | *Take notes and transcribe?* | `autoTranscriptionGeneration` **and** `autoSmartNotesGeneration` |

One flag drives transcript and notes together because for an editor it is one decision — but both are always sent explicitly, since they are independent switches in the API and notes still run with transcription off (verified 2026-09-09). A host can change any of them from inside the call, and the event description states which are on, derived from the flags rather than asserted.

The transcript/notes **language** is not settable through the API: Meet uses the account's *default Meeting Records language*, so set that once for `community@` in Meet settings (English).

## What the API cannot set

The `Space` / `SpaceConfig` field list is exhaustive: `accessType`, `entryPointAccess`, `moderation`, `moderationRestrictions`, `attendanceReportGenerationType`, `artifactConfig`. Anything not in there is not settable from this script, whatever the Meet UI offers. That covers:

| Setting | Only place to change it |
|---|---|
| **Continuous meeting chat** | Not settable per meeting at all. Whether a booking gets it depends on how the conference is made and which calendar it lands on — see [Continuous meeting chat: two independent suppressors](#continuous-meeting-chat-two-independent-suppressors). The org-level switch is Admin console → Apps → Google Workspace → Google Meet → **Meet safety settings**, per OU/group/user; needs Google Chat enabled for the org and defaults to on with *Hosts can modify*. |
| **Allow third-party apps to collect audio and video** | Admin console → Apps → Google Workspace → Google Meet → **Meet safety settings → Media API**. Uncheck *"Let third-party apps that join a Meet call use the audio and video of the call through Meet Media API"*. Can take up to 24h. |
| **Let contributors share add-on activities** | Admin console → Apps → Google Workspace → **Settings for Google Meet → Meet video settings** → add-on visibility (separate toggles for Google and featured third-party add-ons). |
| Notes/transcript **language** | Meet uses the account's *default Meeting Records language*; set it once for `community@` in Meet settings. |
| **Include captions** on a recording | No API field, no known account default. Per-meeting click. |

The two Admin console ones are org-wide and need Super Admin or the *Manage Meet Settings* privilege — an IT ask, not something this script can do.

If `testProvision()` says `FAILED at step "cohost"`, `checkCohostApi()` will tell you which of the three member calls broke and why — a 404 means the Cloud project isn't enrolled in the preview programme or the script isn't pointed at it, a 403 means a missing scope (`meetings.space.settings`; re-accept the consent screen if you added it after authorising). Fix, then run `testResume()`: it picks up the saved partial state and only redoes the co-host step, which also proves the resume path works. A 400 mentioning the email is what a non-Google address looks like — worth seeing once so you recognise it in Slack later.

Both `checkCohostApi()` and `testProvision()` bypass the web app entirely, so they work before anything is deployed.

## Deploy

Two deployments of the same script, because the JSON API and the human form need different access settings:

| Deployment | Execute as | Who has access | Used by |
|---|---|---|---|
| **API** | Me (community@) | Anyone | Umbraco's On-Approve workflow → `doPost`. Protected by `SHARED_SECRET`. |
| **Approver form** | Me (community@) | Anyone within umbraco.dk | The Slack link → `doGet`. `provisionFromForm` re-checks the caller's domain server-side too. **Must** be the organisation-restricted option — with "Anyone", Google hides the caller's identity and the form refuses everyone. |

Deploy → New deployment → Web app for each. Copy the API deployment's URL into Umbraco's `MeetBooking:Google:AppsScript:WebAppUrl` (Cloud portal env var / `appsettings.Local.json`, never the repo). The form URL is what the Slack message links to, with the entry's fields as query params.

Redeploying after a code change: Deploy → Manage deployments → edit → *New version*. The URL stays the same.

### Smoke-test the API deployment

```bash
curl -sL -X POST "$WEBAPP_URL" -H 'Content-Type: application/json' -d '{
  "token": "'"$SHARED_SECRET"'",
  "recordId": "curl-test-1",
  "title": "TEST via curl — delete me",
  "cohostEmail": "you@gmail.com",
  "startLocal": "2026-09-20T10:00:00",
  "durationMinutes": 30,
  "timeZone": "Europe/Copenhagen",
  "recordMeeting": false
}'
```

Or use the Bruno collection in [`bruno/`](bruno/): open the folder in Bruno, pick the `local` environment, set `webAppUrl` to the API deployment URL and `testCohostEmail`, and enter `sharedSecret` as a secret (stored on your machine, not in the repo). *Create test meeting* asserts on the response, *Resume with state* replays it to prove idempotency, *Bad token* checks the gate. Leave "follow redirects" on — Apps Script answers with a 302.

`-L` matters — Apps Script answers with a redirect. Response is `{"ok":true,"state":{...}}`. Delete the event by hand afterwards (it isn't tracked by `cleanupTest()`), or post the same body again with `"state"` from the response to watch every step get skipped.

## Things to know

- **The members endpoints need the canonical `spaces/{id}`.** `spaces.get` accepts a meeting code as an alias, `spaces.members` does not — pass it a code and you get `403 "Permission denied on resource space"`, which reads exactly like a permissions problem and isn't one. `provision_` always uses the resolved `state.spaceName`; anything new calling those endpoints must too.
- **Two different 403s, worth telling apart.** *"Permission denied on resource **Member**"* means the space was created by someone else — that is the one that forced the create-our-own-space design. *"...on resource **space**"* is usually the alias mistake above.
- **Always HTTP 200.** Web apps can't set status codes. The caller must check `ok`.
- **Resumable.** Pass a previous response's `state` back in and the script skips what already succeeded. A retry *without* state is also safe: the event is tagged with `recordId` in a private extended property and `adoptExistingEvent_` finds it. The tag goes through `recordKey_` (lower-cased, hyphens stripped) on both write and lookup, because extended-property matching is exact — the workflow sends `Guid.ToString("N")` while the approver form's URL could carry a hyphenated guid, and unnormalised those two spellings would each get their own booking. (Calendar's `conferenceData.createRequest.requestId` was never an idempotency key — it dedupes only the conference within a single `events.insert` — and the script no longer uses `createRequest` at all.)
- **`testAttachOwnSpace()`** is a kept experiment rather than part of the flow: it re-proves, in about three seconds, that Calendar accepts a space we created and that the co-host call works on it. Re-run it if Google ever changes something and bookings start failing at `cohost` again.
- **Quotas.** Apps Script's daily UrlFetch and Calendar quotas are far above a handful of bookings a week. If we ever hit them, that's a nice problem.
- **Re-authorising.** If community@'s password is reset or the app is revoked, the deployments fail with an auth error. Open the editor as community@, run `showConfig()`, click *Allow* again.
- **Time zones.** `startLocal` is wall-clock in `timeZone`, no offset. End time from `durationMinutes` is wall-clock arithmetic — only wrong for a meeting spanning a DST change at 02:00–03:00 local, which nobody books.

# Community Meet Booking — Design

**Date:** 2026-09-03
**Status:** Approved 2026-09-04. Google leg built and tested (`tools/meet-booking-apps-script/`). Umbraco leg: see [2026-09-04-community-meet-booking.md](2026-09-04-community-meet-booking.md) (implementation plan).
**Goal:** Let community members (user group organisers, package teams, MVPs) request an online meeting from the community site and get a Google Calendar event with a Google Meet where they are **co-host** and the host controls are already set up sensibly — replacing the shared Zoom account we used to hand around.

## Problem

We used to give a handful of community members the login to a shared Zoom account. That meant one set of credentials passed around, no idea who booked what, and a fresh "how do I lock the room" conversation before every meeting. HQ is on Google Workspace, so Google Meet is the obvious replacement — but a Meet is only as good as its host settings, and a member who is handed a bare link ends up with an open room where anyone can present.

We want: member fills in a form → we glance at it and approve → they get a calendar invite with a Meet that is theirs to run, with hosts-only screen sharing, attendance tracking and automatic notes/recording already on.

## Approach

Three pieces, all inside things we already run:

1. **An Umbraco Form** on the community site collects the request. The form has **manual approval** enabled, so every entry sits in state `Submitted` until someone at HQ approves it in the backoffice.
2. **On submit** Forms' built-in *Slack* workflow posts the entry to the community team's channel, so we notice it.
3. **On approve** a custom Forms workflow calls Google: creates the Calendar event with a Meet on a dedicated HQ account, configures the Meet space (hosts-only presenting, attendance tracking, auto notes/recording), adds the member as co-host, and writes the Meet link back onto the entry. The Calendar invite is what emails the member.

This maps onto Forms' native `Submitted → Approved` record states and its "On Submit" / "On Approve" workflow stages ([Forms docs: attaching workflows](https://docs.umbraco.com/umbraco-forms/editor/attaching-workflows)), so the approval step needs no custom UI. The Google calls live in a new Razor Class Library, `src/UmbracoCommunity.MeetBooking/`, following the same shape as `UmbracoCommunity.BlogAnnouncements` (options class, named `HttpClient`s, composer, secrets in `appsettings.Local.json` / Cloud portal — see [`docs/primers/integrations.md`](../primers/integrations.md)).

### Why Forms workflows rather than Umbraco Automate

Automate is what delivers the Discord blog announcements, and it would work here too. But this flow is (a) triggered by a Forms record state change, which Forms already exposes as a workflow stage, and (b) a multi-step Google API sequence that needs to hold state between calls (event id → meeting code → space name). That is a C# workflow type, not a backoffice-editable flow. Keep Automate out of the critical path; if we later want editable Slack message templates, the Slack leg can move into an Automate flow without touching the Google leg.

## What Google gives us (checked against the docs 2026-09-03)

**Calendar API** (`events.insert`) creates the event and, with `conferenceDataVersion=1` and a `conferenceData.createRequest`, a new Meet. The response carries `conferenceData.conferenceId` — the `xxx-xxxx-xxx` meeting code. Attendees on the event get the invite email from Google (`sendUpdates=all`).

**Meet REST API v2** — `spaces.get` accepts the meeting code as an alias for the space name ("Can only be used as an alias of the space name to get the space" — [spaces reference](https://developers.google.com/workspace/meet/api/reference/rest/v2/spaces)). `spaces.patch` then sets `config` ([configuration guide](https://developers.google.com/workspace/meet/api/guides/meeting-spaces-configuration)):

| Field | Values | What we set |
|---|---|---|
| `accessType` | `OPEN` / `TRUSTED` / `RESTRICTED` | `TRUSTED` — the invited requester joins directly, anyone else with the link asks to join and a host admits them. Meet batches simultaneous knocks ("View all" → "Admit all"), so a crowd is one click. Changed from `OPEN` on 2026-09-09; `OPEN` is still available per meeting via the `SPACE_CONFIG` script property |
| `moderation` | `ON` / `OFF` | `ON` — required for co-hosts and the restrictions below to apply |
| `moderationRestrictions.presentRestriction` | `HOSTS_ONLY` / `NO_RESTRICTION` | `HOSTS_ONLY` — only host + co-hosts can share their screen |
| `moderationRestrictions.chatRestriction` | `HOSTS_ONLY` / `NO_RESTRICTION` | `NO_RESTRICTION` |
| `moderationRestrictions.reactionRestriction` | `HOSTS_ONLY` / `NO_RESTRICTION` | `NO_RESTRICTION` |
| `moderationRestrictions.defaultJoinAsViewerType` | `ON` / `OFF` | `OFF` — attendees keep mic and camera; we're not locking those |
| `artifactConfig.*` | auto recording / transcript / smart notes `ON`/`OFF` | **transcript `OFF` and smart notes `OFF`** (both changed 2026-09-09 — a meetup shouldn't be transcribed or summarised unless asked; a host can turn either on in the call); recording follows the form checkbox, which now ships unticked. Recordings land in `community@`'s Drive; the event description tells the co-host to announce it. **Language and "include captions" are not settable via the API** — language falls back to the account's default Meeting Records language, so set that once for `community@`. |
| `attendanceReportGenerationType` | `GENERATE_REPORT` / `DO_NOT_GENERATE` | `GENERATE_REPORT` |
| `entryPointAccess` | `ALL` / `CREATOR_APP_ONLY` | `ALL` (default) — members must be able to join from the normal Meet web/app |

Moderation settings and space config are **Generally Available** (Meet API release notes, April 2025 — [release notes](https://developers.google.com/workspace/meet/release-notes)). Config changes need the `https://www.googleapis.com/auth/meetings.space.settings` scope.

**Meet REST API v2beta — members.** Co-hosts are added with `spaces.members.create` on the `/v2beta/` endpoint: `{ "email": "...", "role": "COHOST" }`. `COHOST` "grants same meeting management abilities as organizer". **This is Developer Preview**, not GA: the Google Cloud project must be enrolled in the [Google Workspace Developer Preview Program](https://developers.google.com/workspace/preview) before the endpoint answers. See risk #1 below.

**Not in the API** (checked against the v2beta schema 2026-09-04): the notes/transcript language, and the add-on switches (*Let contributors share add-on activities*, *Allow third-party apps to collect audio and video*). Those stay manual per meeting unless IT can default them for `community@` in the Admin console — ask. Also not there, and not needed: per-participant camera/mic lock.

### Who is the host

The Meet's organizer is whichever Google account creates the Calendar event. That account must be an HQ Workspace account so the space config is enforceable and recordings/attendance land somewhere we control. We use the existing **`community@umbraco.com`** account — already owned by the community team, not tied to any individual. Because that inbox does other things too, create a **secondary calendar** on it ("Community Meets") and put every event there rather than on the primary calendar, so bookings don't get mixed in with the account's own appointments and can be shared with the team as one calendar. The Meet is still hosted by `community@` — the calendar the event sits on doesn't change who the organizer is.

### How the site talks to Google — two options

**Option A (recommended for v1): Google Apps Script running as the host account.** A small Apps Script project owned by `community@` does all the Google work, and the Umbraco site only ever talks to that script over HTTPS. The script is authorised the normal way — someone signs in as `community@` once, opens the script, and clicks *Allow* on the consent screen for the scopes in its manifest. No service account, no domain-wide delegation, nothing a Workspace super-admin has to grant. The Cloud project involved is the script's own, created by the same account.

- The script uses the built-in **Calendar advanced service** for the event (`Calendar.Events.insert` with `conferenceDataVersion: 1`).
- Meet has **no Apps Script advanced service** ([advanced services list](https://developers.google.com/apps-script/guides/services/advanced)), so the space calls go through `UrlFetchApp` with `Authorization: Bearer ${ScriptApp.getOAuthToken()}` and the Meet scopes declared in `appsscript.json` — the documented pattern for any Google API without a wrapper.
- Deployed as a **web app**, *Execute as: me* (`community@`), *Who has access: Anyone*, protected by a shared secret in the POST body. The Umbraco On-Approve workflow posts the request to it and gets the Meet link back in the response. The site holds one secret (the web app URL + token) instead of a service-account key.
- **Still needs the Developer Preview enrolment for co-hosts.** The `v2beta` members call needs the Meet API enabled on a *standard* GCP project that is enrolled in the preview programme, and the script switched from its default Cloud project to that one. `community@` can normally create its own GCP project — unless IT has locked project creation, which is the one thing to check with them. Verify all of this in the spike.

What we give up versus a service account: the script runs under Apps Script quotas (fine at our volume — a handful of meetings a week), it's a second codebase to keep in the community team's Drive rather than the repo (mitigate: keep the `.gs` source in `tools/meet-booking-apps-script/` and push with `clasp`), and a consent grant that expires if the account's password is reset or the app is revoked (mitigate: the Slack failure message says "re-authorise the script").

**Option B (later, if we outgrow A): service account with domain-wide delegation** impersonating `community@`, calling Calendar and Meet directly from the `UmbracoCommunity.MeetBooking` library. Cleaner runtime, everything in the repo, but needs a Workspace super-admin to create the service account and authorise its client id under Security → API controls → Domain-wide delegation for: `calendar.events`, `meetings.space.created`, `meetings.space.settings`, plus whatever `spaces.members` needs. Broad grants like this are exactly what IT pushes back on, which is why it isn't v1.

Either way the Umbraco side is the same: form, approval, Slack, an On-Approve workflow that hands off a request and stores the result. Only the `Google/` folder of the library differs — in Option A it's a single `AppsScriptMeetProvisioner` doing one HTTPS POST.

### The Apps Script (Option A)

The full script lives in [`tools/meet-booking-apps-script/`](../../tools/meet-booking-apps-script/) — `Code.gs`, `Form.html`, `appsscript.json` and a README with setup, test (`testProvision()` / `cleanupTest()` from the editor) and deployment steps. The core of it:

```js
// appsscript.json → oauthScopes:
//   https://www.googleapis.com/auth/calendar.events
//   https://www.googleapis.com/auth/meetings.space.created
//   https://www.googleapis.com/auth/meetings.space.settings
//   https://www.googleapis.com/auth/script.external_request
// + Calendar advanced service enabled

function doPost(e) {
  const req = JSON.parse(e.postData.contents);
  if (req.token !== PropertiesService.getScriptProperties().getProperty('SHARED_SECRET'))
    return json_(401, { error: 'unauthorised' });

  const state = req.state || {};               // resume support: ids from a previous partial run

  if (!state.eventId) {
    const ev = Calendar.Events.insert({
      summary: req.title,
      description: req.description,
      start: { dateTime: req.startIso, timeZone: req.timeZone },
      end:   { dateTime: req.endIso,   timeZone: req.timeZone },
      attendees: [{ email: req.cohostEmail }],
      conferenceData: { createRequest: { requestId: req.recordId,
                        conferenceSolutionKey: { type: 'hangoutsMeet' } } }
    }, req.calendarId, { conferenceDataVersion: 1, sendUpdates: 'all' });
    state.eventId = ev.id;
    state.meetingCode = ev.conferenceData.conferenceId;
    state.meetUri = 'https://meet.google.com/' + state.meetingCode;
  }

  if (!state.spaceName) {
    state.spaceName = meet_('GET', 'v2/spaces/' + state.meetingCode).name;   // meeting code is a valid alias for get
  }

  if (!state.configured) {
    meet_('PATCH', 'v2/' + state.spaceName + '?updateMask=' + encodeURIComponent(
      'config.accessType,config.moderation,config.moderationRestrictions.presentRestriction'), {
      config: { accessType: 'TRUSTED', moderation: 'ON',
                moderationRestrictions: { presentRestriction: 'HOSTS_ONLY' } }
    });
    state.configured = true;
  }

  if (!state.cohostAdded) {
    meet_('POST', 'v2beta/' + state.spaceName + '/members', { email: req.cohostEmail, role: 'COHOST' });
    state.cohostAdded = true;
  }

  return json_(200, state);
}

function meet_(method, path, body) {
  const res = UrlFetchApp.fetch('https://meet.googleapis.com/' + path, {
    method, contentType: 'application/json', muteHttpExceptions: true,
    headers: { Authorization: 'Bearer ' + ScriptApp.getOAuthToken() },
    payload: body ? JSON.stringify(body) : undefined
  });
  if (res.getResponseCode() >= 300) throw new Error(method + ' ' + path + ' → ' + res.getResponseCode() + ' ' + res.getContentText());
  return JSON.parse(res.getContentText() || '{}');
}
```

Each step is skipped if a previous run already did it, and the returned `state` is what Umbraco stores on the record — so a failure at the co-host step (the flaky one) is retried by re-running the workflow, not by minting a second Meet. The space-config values should come from the request body (populated from `MeetBooking:Google:SpaceConfig`) rather than being hardcoded as above, so the defaults stay editable from the site's config.

**Even less plumbing, if we want to start tomorrow:** deploy the same script with a `doGet` that renders a tiny pre-filled form (query params from the Slack link), *Who has access: anyone in umbraco.dk*. The approver clicks the link in Slack, checks the fields, clicks *Create*, and pastes the Meet link into the Forms entry. No Umbraco code at all beyond the form and the Slack workflow. It's a fine Phase 1 while the Umbraco On-Approve workflow is being built, and the script is the same one.

## The form

Umbraco Form, created in the backoffice and shipped with Forms Deploy like the others. Fields:

| Field | Type | Notes |
|---|---|---|
| Your name | Short answer, required | Goes in the Slack message and the event description |
| Google account email | Email, required | The email we invite and make co-host. **Must be a Google account** (Workspace or gmail.com). Help text says so plainly; we cannot verify it up front — if it isn't, `members.create` fails on approval and Slack tells us (see failure handling) |
| Contact email (if different) | Email, optional | Some people run their user group from a gmail they never read. Used for our confirmation mail only |
| Meeting title | Short answer, required | Becomes the Calendar event summary as-is (no prefix) |
| Purpose / what is it for | Long answer, required | User group meetup, package sync, MVP call, other. Event description + Slack. This is what we sanity-check at approval |
| Date | Date picker, required | Must be in the future; minimum lead time 24 hours so there is time to approve |
| Start time | Time (or dropdown of 15-min slots), required | |
| Duration | Number, minutes, default 60, 5–480 | Free entry rather than a dropdown — 15, 39, whatever |
| Timezone | Dropdown of IANA zones, required, **pre-selected from the browser** | See below |
| Expected attendees | Dropdown: <10 / 10–25 / 25–100 / 100+ | Informational; 100+ may need a Meet plan check with IT |
| Record the meeting? | Checkbox, default **off** | Passed to the script as `recordMeeting`; off → auto-recording `OFF` for that space. Transcription and Gemini notes are both off by default. Help text: recordings are stored on the community account's Drive |
| Anything else | Long answer, optional | |
| Spam guard | `Umbraco.Community.FormsSpamGuard` field | Same as every other public form on the site |
| Consent | Checkbox, required | "We store this to set up your meeting and delete it after N days" — links to the privacy page |

**Timezone.** Ask for it, don't assume CEST — user group organisers are everywhere. The dropdown is a full IANA list with the browser's `Intl.DateTimeFormat().resolvedOptions().timeZone` pre-selected via a small script (progressive enhancement; with JS off it defaults to `Europe/Copenhagen` and says so). The Calendar API takes `start.dateTime` + `start.timeZone` natively, so we pass the member's zone straight through and Google renders it in each invitee's own zone. The Slack message shows both the member's local time and Copenhagen time so the approver doesn't have to convert.

**Submit-side validation** (custom Forms field validation or a pre-submit workflow): future date, lead time, duration set, timezone is a real IANA id.

**Retention.** Turn on Forms' scheduled record deletion for this form ([form settings](https://docs.umbraco.com/umbraco-forms/editor/creating-a-form/form-settings)): Approved and Rejected records deleted after ~90 days, Submitted (never approved) after 30. Confirm the background deletion service is enabled in config — the docs note it is off by default.

**Who can submit.** Decided 2026-09-04: **anyone**, no login. The form is public, protected by the `FormsSpamGuard` field, and every request is human-approved anyway — the approval step is the spam filter of last resort. If junk volume becomes a problem, switching to logged-in members (the site has GitHub member login, `Features/Members/`) is a form setting, not a code change.

## Flow

```
Member submits form
  └─ On Submit workflows (sequential)
       1. Forms built-in "Send email" → member: "got it, we'll confirm within 2 working days"
       2. Forms built-in "Slack" workflow → community team channel (webhook URL is a workflow setting):
          posts the entry's fields
  Record stored as Submitted

HQ opens the entry in Forms, reads it, clicks Approve  (or Reject → nothing further happens*)
  └─ On Approve workflows
       3. CreateMeetWorkflow
            a. Calendar events.insert on community@'s "Community Meets" calendar
               - summary, description (purpose + "how to run your Meet" cheat-sheet)
               - start/end with member's timeZone
               - attendees: [member's Google email]
               - conferenceData.createRequest { requestId = record id, hangoutsMeet }
               - sendUpdates=all
            b. meetingCode = conferenceData.conferenceId
               Meet spaces.get spaces/{meetingCode} → space name
            c. Meet spaces.patch config (table above), updateMask on the fields we set
            d. Meet v2beta spaces/{space}/members.create { email, role: COHOST }
            e. Write back to the record: eventId, meetingCode, meetUri, spaceName (hidden fields)
       4. Forms built-in "Slack" workflow again — the field dump now includes meetLink (or provisioningError)
       5. Optional Forms "Send email" → member with the Meet link + the cheat-sheet
          (Google's own invite email already carries the link; this one adds our tips)
```

\* A Reject should tell the member something. Forms has no "On Reject" stage, so v1 is: the approver replies by email themselves (the Slack message includes a `mailto:`). Revisit if volume justifies it.

### Idempotency and retries

The Google sequence is four calls that can fail halfway. Rules:

- `requestId` on `createRequest` = Forms record id, so a re-run of step (a) doesn't mint a second Meet.
- Step (e) writes back after **each** successful sub-step, and each sub-step first checks the record: eventId present → skip (a); spaceName present → skip (b), and so on. Re-approving (or a "Retry" button, see below) resumes where it stopped.
- Hard failures (auth, quota, 4xx from members.create because the email isn't a Google account) surface in Slack with the step and Google's error text, and the record stays Approved with the partial ids on it, so nobody has to dig in logs.
- No automatic retry loop in v1 — a human sees the Slack message and re-triggers. Umbraco Forms lets you re-run workflows on a record from the entry view, which is the "Retry" button for free.

### Cancellations and changes

Out of scope for v1. If a member needs to move the meeting, they email us and we edit the event in the host account's calendar by hand — the co-host role and space config stay with the space, so a date change is just a Calendar edit. Add a "change request" form later if this actually happens.

## Slack message

**Decided 2026-09-04: use Forms' built-in *Slack* workflow, no code.** Its single setting is the incoming webhook URL, and it posts the entry's field values. Attached twice: On Submit, and On Approve after Create Meet so the post carries the written-back Meet link or error. The webhook therefore lives in the form definition (travels with Forms Deploy), not in appsettings. We lose the designed card below — keep it as the v2 sketch if the field dump proves annoying:

> **New Meet request** from *Name* (`email`)
> **Title** — purpose (first 300 chars)
> **When** 17 Sep 2026, 19:00 Europe/London (20:00 Copenhagen) · 60 min · 10–25 people
> [Open in backoffice]  ·  [Email requester]

**v2 idea, not v1:** Approve/Reject buttons in the Slack message itself. Needs an inbound endpoint on the site verifying Slack's request signature and a Slack app rather than a bare webhook. Nice, but the backoffice Approve button already exists and costs nothing.

## Configuration

```jsonc
"MeetBooking": {
  "Enabled": true,
  "DryRun": false,                       // log what would be created, call nothing — for cutover
  "Google": {
    "HostAccount": "community@umbraco.com",
    "CalendarId": "",                    // id of the "Community Meets" secondary calendar on that account
    "Provider": "AppsScript",            // "AppsScript" (Option A) or "ServiceAccount" (Option B)
    "AppsScript": {
      "WebAppUrl": "",                   // SECRET: Local/portal only
      "SharedSecret": ""                 // SECRET
    },
    "SpaceConfig": {                     // same shape as the Meet API SpaceConfig; passed through to the script
      "accessType": "TRUSTED",
      "moderation": "ON",
      "moderationRestrictions": { "presentRestriction": "HOSTS_ONLY", "chatRestriction": "NO_RESTRICTION",
                                  "reactionRestriction": "NO_RESTRICTION", "defaultJoinAsViewerType": "OFF" },
      "attendanceReportGenerationType": "GENERATE_REPORT",
      "artifactConfig": { "recordingConfig": { "autoRecordingGeneration": "ON" },
                          "transcriptionConfig": { "autoTranscriptionGeneration": "OFF" },
                          "smartNotesConfig": { "autoSmartNotesGeneration": "OFF" } }
    }
  },
  "Form": {
    "FormId": "",                        // the Umbraco Form's GUID, so workflows can find field aliases
    "MinimumLeadTimeHours": 24
  }
}
```

The space config is configuration, not code, so Seb can change the defaults later without a deploy. Anything that is a secret has an empty committed default and a startup check that logs a clear warning when it is missing.

## Project layout

`src/UmbracoCommunity.MeetBooking/` — Razor Class Library, mirrors `UmbracoCommunity.BlogAnnouncements`:

```
MeetBookingOptions.cs
RegisterMeetBooking.cs                 // composer: options, HttpClients, workflow types
Google/
  IMeetProvisioner.cs                  // Provision(MeetRequest, MeetProvisioningState) → MeetProvisioningState
  AppsScriptMeetProvisioner.cs         // Option A: one HTTPS POST to the web app, shared secret
  Dtos/
  // Option B, later: GoogleAuthTokenProvider / GoogleCalendarClient / GoogleMeetClient behind the same interface
Workflows/
  CreateMeetWorkflow.cs                // Forms WorkflowType, On Approve (Slack is Forms' built-in workflow)
Models/
  MeetRequest.cs                       // parsed from the record's fields
  MeetProvisioningState.cs             // eventId / meetingCode / spaceName / cohostAdded
```

With Option A the site needs no Google NuGet packages at all — `AppsScriptMeetProvisioner` is a named `HttpClient` and `System.Text.Json`. The Apps Script source lives in `tools/meet-booking-apps-script/` (`appsscript.json` + `Code.gs`) and is pushed with `clasp` so it's versioned with the rest.

Tests in `tests/UmbracoCommunity.MeetBooking.Tests/`: request parsing from record fields, timezone → UTC conversion edge cases (DST boundaries, half-hour zones), the request/response contract with the web app against fixtures, resume-from-partial-state logic. No live Google calls in CI.

## Phases

**Phase 0 — Spike (do this first, ~half a day).** Before writing any Umbraco code, prove the Google leg end to end with the script in `tools/meet-booking-apps-script/`, signed in as `community@` — its README is the checklist:

1. Ask IT only for confirmation that `community@` may create a GCP project. Nothing else — the account already exists.
2. Create the script, run the Calendar + `spaces.patch` steps from the editor. Confirm the space config (`OPEN`, `HOSTS_ONLY` presenting, attendance report, auto artifacts) actually sticks when a gmail.com invitee joins. **Done 2026-09-04 — worked.**
3. Switch the script to a standard GCP project, enable the Meet REST API, enrol it in the Developer Preview Program. Confirm `POST /v2beta/spaces/{space}/members` works, and specifically whether it accepts an **external** (gmail.com) address as `COHOST`. The docs don't say. If it doesn't, the co-host promise is dead and we're on plan B (risk #1).
4. Confirm the invite email lands for the gmail.com attendee and that they see themselves as co-host on join.

**Phase 1 — Form + Slack + manual create.** Form in the backoffice with the fields above, manual approval on, retention on, Forms' built-in Slack workflow on On Submit, and the script's `doGet` form URL pinned in the channel. The approver clicks Create, pastes the link into the entry. This already replaces the Zoom account and "email Seb" — ship it first.

**Phase 2 — Automatic provisioning on Approve.** `CreateMeetWorkflow` → `AppsScriptMeetProvisioner` → the script's `doPost`, behind `DryRun=true` in staging; flip on live once a couple of real requests have gone through by hand alongside it.

**Phase 3 — Polish.** Cheat-sheet email content, "how to run your Meet" page on the site, Slack approve buttons if we want them.

## Risks and open questions

1. **Members API is Developer Preview.** Google can change or gate it. Plan B is built in: `COHOST_MODE=manual` in the script skips the call and returns instructions; the approver (who has access to `community@`) adds the co-host in Calendar → Meet gear → Co-hosts as part of approving. Everything else — event, invite, host controls — is GA API and unaffected. Not optional in that mode: with hosts-only presenting, a meeting with no co-host and no `community@` present has nobody who can share a screen. Still a big step up from the Zoom account. Plan C: give the member the meeting as **organizer** by creating the event on *their* calendar — impossible without their OAuth consent, so no.
2. **The Apps Script web app is reachable by anyone who has the URL.** The shared secret is the only gate. Mitigations: long random token in Script Properties, rotate if leaked, the script only ever creates events on its own calendar (worst case: junk events on `community@`, no data exposure), and the web app URL itself is treated as a secret. If IT is uncomfortable, fall back to the `doGet` form restricted to `umbraco.dk` accounts and lose the automatic approve step. Option B (service account + domain-wide delegation) has the opposite trade-off — tighter runtime, much bigger up-front grant — so it stays on the shelf until volume justifies it.
3. **"Must be a Google account" will trip people up.** The form help text has to be blunt, and the Slack failure message for a non-Google email should say exactly that so we can reply quickly.
4. **Meet participant cap is 100.** Confirmed on the first real event: `community@` is on an edition with a 100-participant limit, and the cap is set by the *host's* licence — the co-host's or attendees' plans don't matter. Fine for meetups and syncs; anything in the form's "100+" attendee bucket is a flag for the approver. To lift it, IT moves `community@` alone to a higher edition (Business Standard 150, Plus 500, Enterprise 1,000) and every meeting the script creates inherits it.
5. **Data residency.** Form entries live in the site's database on Umbraco Cloud (Azure); Calendar/Meet data lives with Google. Don't promise anything about residency in the form text — link to the privacy page and keep retention short.
6. **Open decisions for Seb:** which existing Slack channel (then create its incoming webhook); retention periods.

## Sources

- [Google Meet REST API — configure meeting spaces and members](https://developers.google.com/workspace/meet/api/guides/meeting-spaces-configuration)
- [Google Meet REST API — spaces resource (v2)](https://developers.google.com/workspace/meet/api/reference/rest/v2/spaces)
- [Google Meet REST API — release notes](https://developers.google.com/workspace/meet/release-notes)
- [Google Meet REST API — overview](https://developers.google.com/workspace/meet/api/guides/overview)
- [google-apps-meet Python types (v2beta Member, Role, AccessType enums)](https://googleapis.dev/python/google-apps-meet/latest/meet_v2beta/types_.html)
- [Apps Script — advanced Google services (and calling APIs without one via UrlFetchApp)](https://developers.google.com/apps-script/guides/services/advanced)
- [Umbraco Forms — attaching workflows (submit / approve stages)](https://docs.umbraco.com/umbraco-forms/editor/attaching-workflows)
- [Umbraco Forms — form settings (moderation, retention)](https://docs.umbraco.com/umbraco-forms/editor/creating-a-form/form-settings)
- [Umbraco Forms — workflow types](https://docs.umbraco.com/umbraco-forms/editor/attaching-workflows/workflow-types)

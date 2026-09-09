# UmbracoCommunity.MeetBooking

The Umbraco half of community Meet booking: an Umbraco Forms **workflow type** that, when HQ approves a meeting
request, asks the community Apps Script to create a Google Calendar event + Google Meet and writes the link back onto
the entry. The Google half lives in [`tools/meet-booking-apps-script/`](../../tools/meet-booking-apps-script/).
Design and rationale: [`docs/plans/2026-09-03-community-meet-booking-design.md`](../../docs/plans/2026-09-03-community-meet-booking-design.md).

## How it fits together

```
requester submits the public form
  On Submit:  Forms built-in "Slack" workflow  → community team channel (field dump)
              Forms built-in "Send email"      → requester: "got it, we'll confirm"
  entry sits in state Submitted

HQ opens the entry in the backoffice, checks it, clicks Approve
  On Approve: Community Meet: create Meet       → POST to Apps Script → Calendar event + Meet + host controls
                                                → writes meetLink / provisioningState / provisioningError on the entry
              Forms built-in "Slack" workflow  → same channel; the field dump now shows the Meet link (or the error)
```

Google emails the requester the invite. Nothing in this project talks to Slack or email — Forms' built-in workflows do.

## Configuration

`appsettings.json` ships the section with empty secrets. Real values go in `appsettings.Local.json` locally and Cloud
portal environment variables on staging/live (`MeetBooking__AppsScript__WebAppUrl`, `MeetBooking__AppsScript__SharedSecret`).

```jsonc
"MeetBooking": {
  "Enabled": true,                 // kill switch: false → the workflow completes without doing anything
  "DryRun": false,                 // true → log the request, write a placeholder link, call nothing
  "AppsScript": {
    "WebAppUrl": "",               // the API deployment's /exec URL (Apps Script → Deploy → Manage deployments)
    "SharedSecret": "",            // equals the script's SHARED_SECRET script property
    "TimeoutSeconds": 60
  },
  "Form": {
    "Aliases": { /* override only if the form's field aliases differ from the defaults below */ }
  }
}
```

A missing `WebAppUrl`/`SharedSecret` logs a warning at startup and fails the workflow with a clear error on the entry.

## The form (build in the backoffice)

Field **aliases** are what the code reads; captions are free — but read the alias rule below before you start typing,
because Forms picks the alias for you.

> **The form is deliberately not in source control.** `Umbraco.Forms.Deploy` is referenced, so form definitions
> *can* be serialised to `forms-form__*.uda` and committed — set `Umbraco:Deploy:Settings:TransferFormsAsContent`
> to `false` (it defaults to `true`, which is why there are no form artifacts in `umbraco/Deploy/Revision`
> alongside the 290-odd others). We keep the default on purpose: **workflow settings are serialised too, and the
> Slack workflows hold live incoming-webhook URLs**, which this repo being public would publish.
>
> The consequence is that the table below is the *only* written record of the form, which is why it is described
> as the contract rather than as documentation: get an alias wrong and the workflow fails at runtime, not at
> compile time. Moving the form between environments goes through the Deploy transfer queue, not git.
>
> If that trade is ever revisited, move the webhook URLs behind configuration and rotate them first. Note the
> switch is awkward to undo: the Deploy docs warn that going `false` → `true` later means deleting the orphaned
> `.uda` files by hand, or they can silently revert a form to an earlier state.

### Setting a field's alias

The **Add field** panel has an alias input to the right of *Name*, behind a small padlock. It auto-derives an alias
from the caption as you type (camelCase, punctuation stripped: `Duration (minutes)` → `durationMinutes`); click the
padlock to unlock it and type the alias you want. So use the friendly caption **and** the alias the code expects —
you don't have to choose.

Set the alias explicitly for every field in the table below. Two supporting facts, in case a form ever drifts:

- The auto-derived alias is only a starting suggestion. Once an alias exists, `Form.SetFieldAliases` **only fills in
  blank aliases — it never overwrites one that is already set**, so rewording a caption later cannot break the mapping.
- If a form does end up with different aliases, they can be remapped wholesale under `MeetBooking:Form:Aliases`
  rather than rebuilding the form.

### Fields

| Caption | Field type | Alias (set explicitly) | Notes |
|---|---|---|---|
| Your name | Short answer, required | `requesterName` | |
| Google account email | **Short answer**, required, *Field Type* = `email` | `googleEmail` | Forms 18 has no "Email" field type — it's Short answer with the **Field Type** setting set to `email`. Help text, blunt: must be a Google account (Gmail or Workspace) — it's invited and made co-host |
| Meeting title | Short answer, required | `meetingTitle` | Used verbatim as the Calendar event's title |
| Meeting description | Long answer, **optional** | `meetingDescription` | Detail for the event body. Optional on purpose: most requests are "organise an online or hybrid meetup" and need no elaboration |
| Date | Date, required | `meetingDate` | Set *Minimum date* to a relative `+1 day` to enforce the booking lead time. The picker (Pikaday) renders two inputs sharing one name — a visible formatted one and a hidden `.datepickerfieldshadow` — so both post; irrelevant for humans, a trap when scripting |
| Start time | Dropdown, required | `startTime` | Prevalues `00:00` … `23:45` in 15-minute steps (`HH:mm`); value and caption the same |
| Duration (minutes) | **Short answer**, required, *Field Type* = `number` | `durationMinutes` | Forms 18 has no "Number" field type either. Range 5–480 is enforced by the workflow |
| Timezone | Dropdown, required | `timeZone` | IANA ids (`Europe/Copenhagen`, `Europe/London`, `America/New_York`, …). Default `Europe/Copenhagen` |
| Record the meeting? | Checkbox | `recordMeeting` | Leave *Default Value* empty so it ships unticked. Set it to exactly `true` to have it ticked — Forms only reads the literal string `true`. An unticked box still stores `false`, so the value is never missing |
| Take notes and transcribe? | Checkbox | `transcribeMeeting` | Drives the transcript **and** Gemini notes together — they are independent switches in the Meet API but one editorial decision. Ships unticked. The notes/transcript **language** is not settable per meeting: Meet uses the host account's *default Meeting Records language*, so set that once for `community@` in Meet settings |
| Spam guard | Spam guard | — | as on every public form. Renders an extra **honeypot decoy** input ("Enquiry reference") alongside its token — leave it empty; anything scripting a submission must too |
| Meet link | **Hidden** | `meetLink` | written by the workflow |
| Provisioning error | **Hidden** | `provisioningError` | written by the workflow, truncated to 255 characters |
| Co-host action | **Hidden** | `cohostAction` | written by the workflow when the co-host has to be added by hand — empty otherwise. See "Co-host" below |

The form deliberately does **not** ask for a contact email, expected attendee count, free-text notes, or a consent
checkbox. They were dropped in favour of a shorter form; none of them were read by the workflow except as extra text
in the event description. If consent capture is reintroduced, note that Forms adds a *Data Consent* field to every
new form by default.

**Settings:** on the form's *Settings* tab, turn on **Moderation → "Enable post moderation"** — that is what holds a
submission in the `Submitted` state for an approver (there is no setting called "Manual approval"). *Store Records* is
on by default.

Retention: the per-form UI offers only *Retain submitted records forever* / *Retain approved records forever* toggles —
there is no per-form day count — so the day figures come from config. Forms' scheduled record deletion is also **off by
default** and the site has no `Umbraco:Forms` section yet, so retention deletes nothing until you add:

```jsonc
"Umbraco": { "Forms": { "Options": { "ScheduledRecordDeletion": { "Enabled": true, "Period": "1.00:00:00" } } } }
```

**Workflows:**

- On Submit: *Slack* (webhook URL of the community team channel), then *Send email* to the requester.
- On Approve: **Community Meet: create Meet** first, then *Slack* again with the same webhook.

Order matters — Forms runs a stage's workflows sequentially, and the second Slack post only carries the Meet link because
Create Meet ran before it. `WorkflowExecutionService` builds each workflow's `WorkflowExecutionContext` around the *same*
`Record` instance, and the built-in `Slack` type renders its message from `context.Record` at execution time, iterating
every record field with no filter — so the hidden fields Create Meet writes in memory do show up in that second post,
captions and all. The webhook URL lives in the form definition and travels with Forms Deploy; rotate it in Slack if it
ever leaks.

## What the workflow does, step by step

1. `Enabled` false → done. Otherwise map the entry to a `MeetRequest` (`Forms/MeetRequestRecordMapper.cs`); a missing or
   malformed field fails the workflow with the alias in `provisioningError`.
2. Read `provisioningState`. Already complete (event, space, config, co-host) → done; nothing is re-sent to Google.
3. Start time already in the past and no event yet → fail with a clear message.
4. `DryRun` → write a placeholder link and finish.
5. POST to the Apps Script with the request and the previous state (`Google/AppsScriptMeetProvisioner.cs`). The script
   skips steps that already succeeded and treats the record id as an idempotency key, so no retry can create a second Meet.
6. Save the state to the `MeetProvisioningStates` table (`Storage/EfMeetStateStore.cs`) **first**, then write
   `meetLink` and `provisioningError` onto the entry — in memory (so the next workflow sees them) and persisted via
   Forms' record storage (`Forms/FormsRecordPersister.cs`). If that second write fails it is logged and swallowed:
   it is the cosmetic half, and the state that prevents a duplicate Meet is already safe.
7. `ok` → Completed. Otherwise Failed: Forms shows it on the entry, and **re-running the workflow from the entry is the retry** —
   it resumes from the saved state. The entry details modal has a *Workflow Audit* table with a **Retry** link on failed
   workflows and **Run Again** on completed ones.

Retrying is **per workflow**, not per stage: the backoffice posts to
`/umbraco/forms/management/api/v1/form/{formId}/record/{recordId}/workflow/{workflowId}/retry`, which resolves that one
workflow and runs it alone. So re-running Create Meet does not re-fire the Slack workflow sitting after it on the Approve
stage. In the other direction, the workflow persisting its own write-back is safe too: Forms' `IgnoreWorkFlowsOnEdit`
defaults to `true`, so saving the record from inside a workflow doesn't re-trigger workflows.

## Where the provisioning state lives

Not on the form. A Forms **Hidden** field has `FieldDataType.String`, which Forms stores in
`UFRecordDataString.Value` — `nvarchar(255)`. A real state object is around 600 characters (the Calendar `htmlLink`
alone is over 100), so writing it there throws *"String or binary data would be truncated"*, and because that SQL runs
inside Umbraco's ambient scope the failure poisons the entire request: the approve returns 500 and the entry never
leaves `Submitted`. Catching the exception does not help — the scope is already broken by then.

So state goes in this package's own table, `MeetProvisioningStates`, keyed by the record's `UniqueId`
(`Storage/`, EF Core, migrated on `UmbracoApplicationStarted` like NotFoundTracker and BlockRestrictions). It uses its
own connection via `IDbContextFactory`, so a problem writing state cannot take the approve request down with it.

It is not the only guard against duplicates, but it is the first one: the Apps Script also tags each event with the
record id and looks for an existing one before inserting (`adoptExistingEvent_`), so even a retry that arrives with no
state at all reuses the event rather than minting a second.

The entry still carries `meetLink` and `provisioningError` as hidden fields, because the approver and the Slack
workflow read them. Both are truncated to 255 characters on write.

## Meeting access and continuous chat

New Meets are created with `accessType: 'TRUSTED'` (in the script's `DEFAULT_SPACE_CONFIG`, overridable via its
`SPACE_CONFIG` property): the invited requester joins directly, anyone else with the link asks to join, and a host or
co-host admits them — in one click for a batch, via *View all* → *Admit all*.

Two consequences worth knowing:

- **Admitting is a host action.** With no co-host added yet (see below), a meeting can start with people knocking and
  nobody able to let them in. `TRUSTED` makes the manual co-host step matter for punctuality, not just screen sharing.
- **Continuous meeting chat never works on these bookings.** Established 2026-09-09 by comparison: the identical
  meeting created by hand in the Calendar UI has it; one booked through the workflow does not — same access type,
  same co-hosts, same external requester, and the event's *Continuous meeting chat* toggle reads on either way. So
  it is not the access type, not the requester being an external Google account, and not that toggle. Something
  about a conference created through the Calendar API does not get its Google Chat conversation provisioned, and we
  found nothing documented about it. There is also no continuous-chat control in the Meet REST API, so it cannot be
  set from here.

  **Given up on purpose, 2026-09-10.** Two independent suppressors: attaching our own Meet space, and putting the
  event on a secondary calendar (which kills chat even for an event created there by hand; moving an event onto one
  loses it too). Having chat would mean `CONFERENCE_MODE=calendar`, which is precisely where the co-host API 403s —
  so an automatic co-host was chosen over chat. A side effect is that `CALENDAR_ID` is unconstrained again, so
  bookings can live on the shareable "Community Meets" calendar. Full measured grid in
  [`tools/meet-booking-apps-script/README.md`](../../tools/meet-booking-apps-script/README.md).

## Co-host

Co-hosts **are** automated. `spaces.members` (Developer Preview, `/v2beta/`) only works on a Meet space the calling
app created, so the Apps Script creates the space itself (`CONFERENCE_MODE=own-space`) and attaches it to the event.
`COHOST_MODE=api`, and the state comes back with `cohostAdded: true`. External addresses work — verified with a
personal gmail, which is what most requesters have.

That choice costs continuous meeting chat, which needs Calendar to mint the conference — the one case where the
co-host call 403s. The trade is decided and documented in
[`tools/meet-booking-apps-script/README.md`](../../tools/meet-booking-apps-script/README.md#conference-mode-continuous-chat-vs-automated-co-hosts);
it is a script-property change, not a code change, if it is ever revisited.

An automatic co-host is also what makes `presentRestriction: HOSTS_ONLY` safe: hosts-only presenting needs somebody
in the room who can present, and that is the requester. The two settings hold each other up.

Nothing on this side needs configuring, and all four co-host outcomes are handled:

- `cohostAdded` → nothing to do; both hidden fields stay empty. This is the normal case.
- `cohostSkipped` (`COHOST_MODE=off`) → same, and it counts towards `IsComplete`. That last part matters: without
  it a finished booking would look unfinished forever and every re-run would call Google again.
- `cohostManual` → the outstanding task goes into the **`cohostAction`** hidden field, which puts it on the entry
  in the backoffice and in the Slack post. The workflow still reports *Completed*, so that field is the only
  signal a human owes us something.
- `cohostError`, set when `auto` mode gave up on the API, is written to **`provisioningError`** even though the
  booking succeeded — otherwise a degraded co-host is indistinguishable from a deliberately manual one.

In the modes that do call the API, re-running the workflow retries the step safely: the script lists the space's
members first, so an already-added co-host is skipped rather than re-created, and a booking that fell back to
manual picks its co-host up once the API works again.

The manual fallback for the whole flow is the script's own approver form (see the Apps Script README).

## Slack

Approval posts one line, via this package's own **Community Meet: post to Slack** workflow type rather than Forms'
built-in Slack workflow. The built-in one takes only a webhook URL and always dumps every field on the entry, which
for this form is a dozen lines hiding the two that matter.

```
✅ Community meeting <event link|created> for Owain Jones (Community hour).
someone@gmail.com has been invited and made a co-host, so they can start the Meet on their own.
```

- Two lines: what happened, then what it means for whom. Only the word **created** carries the link, so the first
  line reads as a sentence rather than a banner.
- **The Meet link is deliberately not posted.** The meeting is usually weeks away and a Meet link in a channel is
  something people click. The link goes to the *calendar event* instead — the thing a reader might actually want
  to open, to check the time or adjust co-hosts.
- No requester name on the entry falls back to the title alone (`… created for Community hour.`).
- The second line states what happened, and there are three of them: co-host added automatically (above);
  invited but **not** a co-host yet, followed by the Apps Script's `cohostInstructions` **verbatim** so that
  wording lives in one place; or simply invited, when `COHOST_MODE=off`.
- A booking that failed still posts — *"⚠️ Community meeting **not** created for X — check the entry"* — because
  replacing the built-in workflow must not make failure silent.
- The title, the requester name and the email are all Slack-escaped (`&`, `<`, `>`, `|`), since all three are
  requester-supplied and an unescaped `|` would end a link label and let the rest become a different link target.

Attach it on the **Approve** stage *after* Create Meet, since it reads the state that workflow wrote. Settings: the
webhook URL only — the channel is fixed by the webhook. Keep using the built-in Slack workflow for the *Submitted*
stage notification, which genuinely does want the whole entry.

## Local development

```jsonc
// src/UmbracoCommunity.Web.UI/appsettings.Local.json (gitignored)
"MeetBooking": { "AppsScript": { "WebAppUrl": "https://script.google.com/macros/s/…/exec", "SharedSecret": "…" } }
```

Point the form's Slack workflows at a test channel's webhook. Then: submit → approve → Meet link on the entry → re-run the
workflow → same link, no second event. Set `SharedSecret` to something wrong to see the failure path.

Tests: `dotnet test tests/UmbracoCommunity.MeetBooking.Tests` — mapper (typed and string field values, validation,
write-back), provisioner (contract, resume state, non-JSON and network failures), workflow (idempotency, dry run, failure
paths). No live Google calls.

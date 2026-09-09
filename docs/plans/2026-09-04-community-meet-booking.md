# Community Meet Booking — Implementation Plan (Umbraco side)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let a community member request a Google Meet from a public Umbraco Form; HQ gets a Slack ping (Forms' built-in *Slack* workflow), approves the entry in the backoffice, and a custom On-Approve workflow calls the already-built Apps Script API to create the Calendar event + Meet and writes the link back onto the entry.

**Design:** [2026-09-03-community-meet-booking-design.md](2026-09-03-community-meet-booking-design.md). The Google leg (`tools/meet-booking-apps-script/`) is **done and tested** — this plan is only the Umbraco half.

**Architecture:** A new Razor Class Library `src/UmbracoCommunity.MeetBooking/` registering **one** Umbraco Forms workflow type, `CreateMeetWorkflow` (On Approve). The Google work is one HTTPS POST to the Apps Script web app behind an `IMeetProvisioner` interface. Slack is Forms' **built-in *Slack* workflow** (webhook URL is a workflow setting on the form — no code, no config key): once On Submit for the "new request" ping, once On Approve *after* Create Meet so the post carries the written-back `meetLink` / `provisioningError` fields. The form itself is content (built in the backoffice, shipped with Forms Deploy), not code.

**Tech stack:** .NET 10, Umbraco CMS 18.1, Umbraco Forms 18.0 (`Umbraco.Forms.Core` / `.Core.Providers` already in `Directory.Packages.props`), `System.Text.Json`, xUnit + FluentAssertions + Moq (as in `tests/Umbraco.Community.FormsSpamGuard.Tests`). No new NuGet packages.

---

## Reference files (read before starting)

- `src/Umbraco.Community.FormsSpamGuard/FormsSpamGuardComposer.cs` — a self-registering Forms extension composer (`builder.FormsFields().Add<>()`); ours does the same with workflows.
- `src/Umbraco.Community.FormsSpamGuard/Umbraco.Community.FormsSpamGuard.csproj` — RCL csproj shape with Forms package refs.
- `src/UmbracoCommunity.BlogAnnouncements/RegisterBlogAnnouncements.cs` — options binding, named `HttpClient` with timeout + User-Agent.
- `tests/Umbraco.Community.FormsSpamGuard.Tests/TestHelpers.cs` — test helper conventions.
- `tools/meet-booking-apps-script/Code.gs` (header comment) — the request/response contract we call.
- `docs/primers/integrations.md` — "Adding a new integration" rules (options, named client, secrets out of git).
- `CODE_CONVENTIONS.md`.

## The contract we call (from `Code.gs`)

POST `{WebAppUrl}` (follow the 302; .NET's `HttpClient` does, switching to GET, which Apps Script expects) with JSON:

```jsonc
{ "token": "<SharedSecret>", "recordId": "<forms record id>", "title": "...", "description": "...",
  "requesterName": "...", "cohostEmail": "...", "startLocal": "2026-09-17T19:00:00", "durationMinutes": 60,
  "timeZone": "Europe/London", "recordMeeting": true, "state": { ...previous partial state or omitted... } }
```

Always HTTP 200. Body: `{ "ok": true, "state": { eventId, htmlLink, meetingCode, meetUri, spaceName, configured, cohostAdded | cohostManual+cohostInstructions } }` or `{ "ok": false, "step": "calendar|space|config|cohost", "error": "...", "state": {...partial} }`. Passing `state` back resumes; `recordId` is the idempotency key on Google's side.

## File structure

**New project `src/UmbracoCommunity.MeetBooking/`** (Razor Class Library, no views, no client):
- `UmbracoCommunity.MeetBooking.csproj`
- `MeetBookingOptions.cs` — bound to `MeetBooking` section.
- `RegisterMeetBooking.cs` — `IComposer`: options, HttpClients, services, `builder.WithCollectionBuilder<WorkflowCollectionBuilder>().Add<…>()`.
- `Models/MeetRequest.cs` — what the form said, typed.
- `Models/MeetProvisioningState.cs` — mirrors the script's `state` object.
- `Models/ProvisionResult.cs` — `Ok`, `Step`, `Error`, `State`.
- `Forms/MeetRequestFieldAliases.cs` — the form field aliases in one place (configurable, with defaults).
- `Forms/MeetRequestRecordMapper.cs` — `Record` → `MeetRequest`; reads/writes the hidden result fields.
- `Google/IMeetProvisioner.cs`
- `Google/AppsScriptMeetProvisioner.cs` — one POST, parse, no retries (the workflow is the retry).
- `Google/AppsScriptHttpClient.cs` — typed-client marker.
- `Workflows/CreateMeetWorkflow.cs`

**New tests `tests/UmbracoCommunity.MeetBooking.Tests/`:**
- `UmbracoCommunity.MeetBooking.Tests.csproj`
- `MeetRequestRecordMapperTests.cs`, `AppsScriptMeetProvisionerTests.cs`, `CreateMeetWorkflowTests.cs`, `TestHelpers.cs`.

**Modified:**
- `UmbracoCommunity.sln` — add both projects.
- `src/UmbracoCommunity.Web.UI/UmbracoCommunity.Web.UI.csproj` — `ProjectReference` to the RCL.
- `src/UmbracoCommunity.Web.UI/appsettings.json` — `MeetBooking` section with empty secrets.
- `CLAUDE.md` — solution structure list (8 projects now) + one-liner.
- `docs/primers/integrations.md` — add to "the map".

---

## Task 1: Project scaffold

**Files:** new csproj ×2, `UmbracoCommunity.sln`, `UmbracoCommunity.Web.UI.csproj`.

- [ ] **Step 1: RCL csproj** — copy `Umbraco.Community.FormsSpamGuard.csproj`, rename, drop `AddRazorSupportForMvc` and the `Client\**` items (no views, no client). Package refs: `Umbraco.Cms.Web.Website`, `Umbraco.Forms.Core`, `Umbraco.Forms.Core.Providers`. Add `<InternalsVisibleTo Include="UmbracoCommunity.MeetBooking.Tests" />`.
- [ ] **Step 2: Test csproj** — copy `Umbraco.Community.FormsSpamGuard.Tests.csproj`, point the `ProjectReference` at the new RCL.
- [ ] **Step 3: Solution + host reference** — `dotnet sln UmbracoCommunity.sln add src/UmbracoCommunity.MeetBooking tests/UmbracoCommunity.MeetBooking.Tests`; add `<ProjectReference Include="..\UmbracoCommunity.MeetBooking\UmbracoCommunity.MeetBooking.csproj" />` next to the FormsSpamGuard one in `UmbracoCommunity.Web.UI.csproj`.
- [ ] **Step 4:** `dotnet build` — green with empty projects.

## Task 2: Options + appsettings

**Files:** `MeetBookingOptions.cs`, `appsettings.json`.

- [ ] **Step 1: Options class**

```csharp
namespace UmbracoCommunity.MeetBooking;

public sealed class MeetBookingOptions
{
    public const string SectionName = "MeetBooking";

    public bool Enabled { get; set; } = true;

    /// <summary>Log what would be sent to Google and Slack, send nothing. For cutover.</summary>
    public bool DryRun { get; set; }

    public AppsScriptOptions AppsScript { get; set; } = new();
    public FormOptions Form { get; set; } = new();

    public sealed class AppsScriptOptions
    {
        /// <summary>The API deployment's /exec URL. SECRET-ish: appsettings.Local.json / Cloud portal only.</summary>
        public string WebAppUrl { get; set; } = "";
        /// <summary>Must equal the script's SHARED_SECRET property. SECRET.</summary>
        public string SharedSecret { get; set; } = "";
        public int TimeoutSeconds { get; set; } = 60;   // Apps Script cold starts + 4 Google calls; 30 is too tight
    }

    public sealed class FormOptions
    {
        public int MinimumLeadTimeHours { get; set; } = 24;
        /// <summary>Field aliases on the Umbraco Form. Defaults match the form described in Task 10.</summary>
        public MeetRequestFieldAliases Aliases { get; set; } = new();
    }
}
```

- [ ] **Step 2: appsettings.json** — add after `CommunityBlogs`:

```jsonc
"MeetBooking": {
  "Enabled": true,
  "DryRun": false,
  "AppsScript": { "WebAppUrl": "", "SharedSecret": "", "TimeoutSeconds": 60 },
  "Form": { "MinimumLeadTimeHours": 24 }
}
```

Real values go in `appsettings.Local.json` locally and Cloud portal environment variables (`MeetBooking__AppsScript__SharedSecret` etc.) on staging/live. Never the repo.

## Task 3: Models + field aliases

**Files:** `Models/*.cs`, `Forms/MeetRequestFieldAliases.cs`.

- [ ] **Step 1:** `MeetRequest` record: `RecordId` (Guid), `RequesterName`, `CohostEmail`, `ContactEmail?`, `Title`, `Description`, `StartLocal` (DateTime, Kind=Unspecified — wall clock), `DurationMinutes`, `TimeZone` (IANA string), `ExpectedAttendees?`, `RecordMeeting` (bool), `Notes?`.
- [ ] **Step 2:** `MeetProvisioningState` class with the script's fields, all nullable/optional, `[JsonPropertyName]` camelCase, plus `ToJson()` / `FromJson()` helpers (this is what we persist on the record as one string).
- [ ] **Step 3:** `ProvisionResult` record: `bool Ok`, `string? Step`, `string? Error`, `MeetProvisioningState State`.
- [ ] **Step 4:** `MeetRequestFieldAliases` with string properties and these defaults — they are the form field aliases Task 9 creates:

| Property | Default alias | Form field |
|---|---|---|
| `RequesterName` | `requesterName` | Your name |
| `CohostEmail` | `googleEmail` | Google account email |
| `ContactEmail` | `contactEmail` | Contact email (optional) |
| `Title` | `meetingTitle` | Meeting title |
| `Description` | `purpose` | Purpose |
| `Date` | `meetingDate` | Date (Forms date picker — date only) |
| `StartTime` | `startTime` | Start time (`HH:mm`, dropdown of 15-min slots) |
| `DurationMinutes` | `durationMinutes` | Duration (number) |
| `TimeZone` | `timeZone` | Timezone (dropdown, IANA ids) |
| `ExpectedAttendees` | `expectedAttendees` | Expected attendees |
| `RecordMeeting` | `recordMeeting` | Record the meeting? (checkbox) |
| `Notes` | `notes` | Anything else |
| `MeetLink` | `meetLink` | *hidden* — written back |
| `ProvisioningState` | `provisioningState` | *hidden* — JSON, written back |
| `ProvisioningError` | `provisioningError` | *hidden* — last error, written back |

## Task 4: Record mapper

**Files:** `Forms/MeetRequestRecordMapper.cs`, tests.

- [ ] **Step 1: Read** — `MeetRequest Map(Record record, Form form)`: for each alias `record.GetRecordFieldByAlias(alias)?.ValuesAsString(false)`. Combine `Date` + `StartTime` into `StartLocal` (`DateTime.ParseExact` on the date part — Forms stores date-picker values as ISO; confirm the exact string during the spike and pin it in a test). Checkbox → `RecordMeeting` (Forms stores `"true"`/`"on"`/`"1"` depending on field type — accept all). Missing required field → `MeetRequestMappingException` naming the alias, so a misconfigured form fails loudly in the workflow log, not with a null ref.
- [ ] **Step 2: State** — `MeetProvisioningState? ReadState(Record)` from the hidden field (null when empty/invalid JSON), `void WriteResult(Record, ProvisionResult)` sets `meetLink`, `provisioningState`, `provisioningError`.
- [ ] **Step 3: Persisting** — after `WriteResult`, the workflow must save the record. Forms exposes `IRecordStorage.UpdateRecordAsync(Record, Form)` (Core.Data.Storage). **Spike this first**: confirm the method on the installed 18.0.6 package and that updating a record from inside an On-Approve workflow doesn't re-fire workflows or reset the state. If it does, fall back to storing the state in a small EF table (the NotFoundTracker pattern) keyed by record id — same interface, different store. Don't guess; a 20-minute check.
- [ ] **Step 4: Tests** — happy path; each missing required alias throws with the alias in the message; date/time combination; checkbox variants; state round-trip; `ReadState` on garbage returns null.

## Task 5: Apps Script provisioner

**Files:** `Google/*.cs`, tests.

- [ ] **Step 1:** `IMeetProvisioner { Task<ProvisionResult> ProvisionAsync(MeetRequest request, MeetProvisioningState? previousState, CancellationToken ct); }`
- [ ] **Step 2:** `AppsScriptMeetProvisioner(AppsScriptHttpClient http, IOptionsMonitor<MeetBookingOptions> options, ILogger)` — builds the request body (token, recordId as `N` string, `startLocal` as `yyyy-MM-ddTHH:mm:ss`, `state` only when non-null), POSTs with `System.Text.Json`, reads the body regardless of status, deserialises `ok/step/error/state`. Non-JSON body (Google sign-in page = wrong deployment access, or a 4xx HTML) → `ProvisionResult` with `Ok=false`, `Step="transport"`, first 300 chars of the body in `Error`. Never throws for a bad response; throws only for cancellation.
- [ ] **Step 3:** Registration in the composer: `AddHttpClient<AppsScriptHttpClient>(c => { c.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds); c.DefaultRequestHeaders.UserAgent.ParseAdd("UmbracoCommunitySite/1.0 (+https://community.umbraco.com)"); })`. Redirects: default handler follows and converts POST→GET on 302, which is exactly how Apps Script serves the response — leave defaults alone, but pin it with a test using a `StubHandler` that returns 302 then 200 (or note that `HttpClient`'s redirect handling is outside the stubbed handler and test the parsing only).
- [ ] **Step 4:** Tests with a `StubHandler`: ok body → `Ok=true` and full state; failed body → `Ok=false`, step, partial state; HTML body → transport error; previous state is serialised into the request; `recordMeeting` and `durationMinutes` land in the body.

## Task 6: Slack — built-in workflow, no code

Forms' built-in **Slack** workflow type takes one setting, the incoming webhook URL, and posts the entry's field values to the channel. We use it twice (Task 10 step 3). Two consequences to accept:

- The message is Forms' default field dump, not a designed Block Kit card: no "open entry" button, no Copenhagen-time conversion. The entry has everything; the approver opens the backoffice anyway. If this grates after a few weeks, a custom workflow (Block Kit, entry link, dual timezone) is a contained addition — the design doc describes it — but it is not v1.
- The webhook URL is stored **in the form definition**, so it travels with Forms Deploy artifacts between environments and lives in the Cloud repo's Deploy data rather than in config. That's how every Forms Slack workflow works; note it in the README so nobody is surprised, and rotate the webhook if the repo ever leaks.

- [ ] **Step 1 (spike item):** confirm that an On-Approve Slack workflow placed *after* Create Meet sees the `meetLink` / `provisioningError` values Create Meet wrote to the record in the same stage. If the second workflow reads a stale record, Create Meet must update `context.Record` in memory as well as persisting — cheap to do either way, so do both.

## Task 7: (removed — folded into Task 6)

## Task 8: `CreateMeetWorkflow`

**Files:** `Workflows/CreateMeetWorkflow.cs`, tests.

- [ ] **Step 1:** Skeleton:

```csharp
public sealed class CreateMeetWorkflow : WorkflowType
{
    public CreateMeetWorkflow(IMeetProvisioner provisioner, MeetRequestRecordMapper mapper, IRecordStorage records,
                              IOptionsMonitor<MeetBookingOptions> options, TimeProvider clock, ILogger<CreateMeetWorkflow> logger)
    {
        Id = new Guid("6f2c1b0a-7d0e-4c3a-9b3e-2e4c0a1d5f02");
        Name = "Community Meet: create Meet";
        Description = "Creates the Google Calendar event + Meet via the community Apps Script and writes the link back onto the entry. Attach on the Approve stage.";
        Icon = "icon-video";
        Group = "Community Meet";
        // ...
    }

    public override Task<WorkflowExecutionStatus> ExecuteAsync(WorkflowExecutionContext context) { /* Step 2 */ }
    public override List<Exception> ValidateSettings() => [];
}
```

The exact `WorkflowType` members (`Id`/`Name`/`Description`/`Icon`/`Group` setters, `ExecuteAsync(WorkflowExecutionContext)`, `WorkflowExecutionStatus.Completed|Failed`) are per the Forms 18 extending docs; confirm they compile against 18.0.6 in the spike. No `[Setting]`s needed.
- [ ] **Step 2: Execute**
  1. `Enabled` false → `Completed`.
  2. `request = _mapper.Map(...)`; mapping exception → write `provisioningError`, save, log, return `Failed`.
  3. Lead-time check is **not** repeated here (the approver just looked at the date); only reject if `StartLocal` is in the past.
  4. `previous = _mapper.ReadState(record)`; if `previous?.EventId != null && (previous.CohostAdded || previous.CohostManual) && previous.Configured` → already done, return `Completed` (a re-approval must not re-post to Google).
  5. DryRun → log the request, write a fake state with `meetUri = "https://meet.google.com/dry-run"`, return `Completed`.
  6. `result = await _provisioner.ProvisionAsync(request, previous, ct)`.
  7. `_mapper.WriteResult(record, result)` (updates `context.Record` in memory, so the built-in Slack workflow that runs next sees it); save via the storage decided in Task 4 step 3.
  8. `result.Ok` → `Completed`; else → `Failed` (Forms marks the workflow failed on the entry and the backoffice lets you re-run it — that's the retry button; the state written in step 7 makes the re-run resume).
- [ ] **Step 3:** Requester email with the link + cheat-sheet is **not** code: the Calendar invite already carries the link and description. If we want a branded mail later, attach Forms' built-in *Send email* workflow after this one on the Approve stage, using the `meetLink` field.
- [ ] **Step 4: Tests** (mock `IMeetProvisioner`, mapper on a hand-built `Record`): happy path writes state + returns Completed; provisioner failure writes partial state + error + returns Failed; already-complete state short-circuits without calling the provisioner; previous partial state is passed through; DryRun never calls the provisioner.

## Task 9: Composer

**Files:** `RegisterMeetBooking.cs`.

- [ ] Bind options; `TryAddSingleton(TimeProvider.System)`; `AddHttpClient<AppsScriptHttpClient>`; `AddSingleton<MeetRequestRecordMapper>()`, `AddSingleton<IMeetProvisioner, AppsScriptMeetProvisioner>()`; `builder.WithCollectionBuilder<WorkflowCollectionBuilder>().Add<CreateMeetWorkflow>()`. Self-registering like FormsSpamGuard (a project reference is enough). Startup: log a warning (not throw) when `Enabled` and `WebAppUrl`/`SharedSecret` are empty.

## Task 10: The form (backoffice, not code)

Build once on local, verify, then transfer with Forms Deploy (or rebuild on live — it's ten minutes). Document in the RCL's README.

- [ ] **Step 1: Fields** (alias exactly as Task 3; Umbraco Forms shows the alias under the field's settings):

| Field | Type | Alias | Notes |
|---|---|---|---|
| Your name | Short answer, required | `requesterName` | |
| Google account email | Email, required | `googleEmail` | Help text, blunt: "Must be a Google account (Gmail or Google Workspace). This is the account we invite and make co-host." |
| Contact email if different | Email | `contactEmail` | |
| Meeting title | Short answer, required | `meetingTitle` | Used as-is for the event title |
| What is the meeting for? | Long answer, required | `purpose` | Multi-line OK |
| Date | Date, required | `meetingDate` | |
| Start time | Dropdown, required | `startTime` | `00:00`…`23:45` in 15-min steps, value = label |
| Duration (minutes) | Number, required, default 60 | `durationMinutes` | Free entry; validation 5–480 |
| Timezone | Dropdown, required | `timeZone` | IANA ids. Default `Europe/Copenhagen`. Optional JS enhancement to preselect the browser zone — Task 11 |
| Expected attendees | Dropdown | `expectedAttendees` | `Fewer than 10` / `10–25` / `25–100` / `More than 100` — the last one is a flag: the host account's cap is 100 |
| Record the meeting? | Checkbox, default checked | `recordMeeting` | Help text: notes and transcript are always generated; recording is stored on the community account's Drive |
| Anything else | Long answer | `notes` | |
| Consent | Checkbox, required | `consent` | "We use this to set up your meeting and delete it after 90 days" + privacy link |
| Spam guard | Spam Guard field | — | as on every public form |
| Meet link | Hidden | `meetLink` | written by the workflow |
| Provisioning state | Hidden | `provisioningState` | written by the workflow |
| Provisioning error | Hidden | `provisioningError` | written by the workflow |

- [ ] **Step 2: Settings** — *Manual approval* **on**; *Store records* on; retention: Submitted 30 days, Approved 90, Rejected 30 (confirm the scheduled record deletion service is enabled in `Umbraco:Forms` config — the docs say it's off by default).
- [ ] **Step 3: Workflows** — On Submit: Forms' built-in **Slack** (webhook URL of the community team channel), then Forms' *Send email* to the requester ("Got it — we'll confirm within two working days"). On Approve: **Community Meet: create Meet** first, then a second built-in **Slack** with the same webhook — its field dump now includes `meetLink` (or `provisioningError`). Order matters; Forms runs a stage's workflows sequentially in the configured order.
- [ ] **Step 4: Message on submit** — plain, tells them what happens next and the Google-account requirement once more.
- [ ] **Step 5: Page** — a content page with the form (Forms macro/block as used elsewhere on the site) plus a short "Running your Meet" explainer above it (hosts-only presenting, recording on by default, 100-participant cap).

## Task 11: Frontend nicety (optional, small)

- [ ] A few lines of TypeScript in `StaticAssets`: on the booking page, if `Intl.DateTimeFormat().resolvedOptions().timeZone` is in the `timeZone` dropdown, preselect it. Progressive enhancement; with JS off the default stands.

## Task 12: Docs and wiring

- [ ] `CLAUDE.md`: solution structure list → 8 projects, one line for `UmbracoCommunity.MeetBooking`.
- [ ] `docs/primers/integrations.md`: add a row to "the map" (the RCL; type: outbound POST to Apps Script; config `MeetBooking`; secrets `WebAppUrl`, `SharedSecret`). Slack is a Forms workflow setting, not an integration in this sense — mention it in the RCL README instead.
- [ ] `src/UmbracoCommunity.MeetBooking/README.md`: how the workflows are attached, the field aliases, config keys, how to retry a failed entry, link to the Apps Script README.
- [ ] Design doc status line → "Built".

## Task 13: End-to-end on local, then staging

- [ ] Local: `appsettings.Local.json` with the real `WebAppUrl` + `SharedSecret` (from Script Properties); the form's Slack workflows pointed at a *test* channel's webhook. Submit the form → Slack ping → approve in backoffice → Meet link on the entry → second Slack post shows the link. Re-run the Create workflow on the same entry → no second event (idempotent). Break the secret → workflow Failed, error on the entry and in Slack; fix, re-run → resumes.
- [ ] Staging: `DryRun=true` first, Slack workflows switched to the real channel's webhook; then `DryRun=false` with a real request from Seb's gmail. Delete the test events afterwards.
- [ ] Live: flip on. Keep the Apps Script approver form as the manual fallback and link it from the RCL README.

---

## Spike findings (2026-09-07, against Umbraco.Forms 18.1.1 by reflection)

1. **Workflow API** — `WorkflowType` has settable `Id/Name/Description/Icon/Group/Alias`, `Task<WorkflowExecutionStatus> ExecuteAsync(WorkflowExecutionContext)`, `List<Exception> ValidateSettings()`. Statuses: `Completed`, `Failed`, `NotConfigured`, `Cancelled`, `SkippedDueToConditions`, `Pending`. Registration: `builder.FormsWorkflows().Add<T>()` (`Umbraco.Forms.Core.Providers.Extensions`). Constructor injection works (the built-in `SlackV2` takes `ILogger`, `IHttpClientFactory`).
2. **Persistence** — `IRecordStorage.UpdateRecord(Record, Form)` is synchronous and (per decompile) deletes and re-inserts every record field row from `record.RecordFields`, so setting `RecordField.Values` in memory and calling it persists the hidden fields. Wrapped in `IRecordPersister` so the workflow is testable. **Still to confirm on a running site (Task 13):** that re-running an Approve-stage workflow from the backoffice doesn't re-fire the whole stage.
3. **Stored values** — at submit time `DatePicker.ConvertToRecord` yields a `DateTime`, `CheckBox` a `bool` (only the literal `"true"` → `true`; `"on"` is false — Forms' own checkbox posts `true`). `RecordField.ValuesAsString` renders dates culture-formatted (`09/17/2026 00:00:00`), so the mapper reads `Values[0]` directly and accepts `DateTime`/`bool` objects plus ISO strings; ambiguous `dd/MM` strings are rejected on purpose.
4. **Slack** — the built-in type is `SlackV2` with one setting, *Webhook URL*. Whether a second Slack workflow on the Approve stage sees Create Meet's write-back is a Task 13 check; Create Meet updates `context.Record` in memory as well as persisting, which is what makes it work if the stage shares the record instance.

Tasks 1–5, 8, 9 and the docs half of 12 are built and unit-tested (60 tests) on branch `feature/community-meet-booking`; the form (Task 10), the optional JS (11) and the end-to-end runs (13) remain.

## Second pass (2026-09-07, in the full solution)

`dotnet build` on the whole solution and `dotnet test tests/UmbracoCommunity.MeetBooking.Tests` are both green with no
changes — the spike had already been run against 18.1.1, the version `Directory.Packages.props` pins, so nothing about
building inside the solution moved.

Four corrections to Task 10's field table, all now folded into the RCL README:

1. **Aliases are editable — set them explicitly.** The **Add field** panel has an alias input beside *Name*, behind a
   padlock: it auto-derives from the caption (`Form.GenerateFieldAlias`, camelCase, punctuation stripped), and clicking
   the padlock unlocks it for a manual value. Verified in the running backoffice: caption "Your name" + alias
   `requesterName`. So the friendly captions and the code's aliases can both be had.

   (An earlier pass through this file claimed there was no alias editor, on the strength of grepping the Forms
   `staticwebassets` bundle for an `Alias` label and finding none. That was wrong — the input is contributed by a shared
   CMS core component, not the Forms bundle, so the grep could never have found it. The related finding does still hold
   and is what makes the alias durable: `SetFieldAliases` **only fills a blank alias, never overwriting an existing
   one**, so rewording a caption later cannot break the mapping.)
2. **There is no "Number" field type — nor an "Email" one.** The field-type picker offers Short answer, Long answer,
   Date, Checkbox, File upload, Password, Title and description, Rich text, Hidden, Dropdown, Multiple choice, Single
   choice, Data Consent, Spam guard and the reCAPTCHAs. Duration and both email fields are Short answers with the
   *Field Type* setting set to `number` / `email` (that setting's prevalues are
   `date,datetime-local,email,tel,text,number,time,url,week`).
3. **Consent is the dedicated "Data Consent" field type**, not a plain Checkbox — and Forms adds one to every new form
   automatically, pre-captioned "Consent for storing submitted data" with the alias `dataConsent`. Keep it rather than
   adding your own.
4. **Retention needs a config key the site doesn't have yet.** `ScheduledRecordDeletion.Enabled` defaults to `false` and
   there's no `Umbraco:Forms` section in `appsettings.json`, so `Umbraco:Forms:Options:ScheduledRecordDeletion:Enabled`
   has to be added or the retention settings delete nothing.

Both open checks from the first spike are answered by the package, and both the way we hoped:

- **Does the second Slack workflow on Approve see Create Meet's write-back?** Yes. `WorkflowExecutionService`
  `ExecuteWorkflowsAsync` loops the stage's workflows and constructs `new WorkflowExecutionContext(record, …)` around the
  *same* `Record` instance every iteration; `SlackV2.ExecuteAsync` calls
  `GenerateNotificationMessage(context.Record, context.Form)` at execution time, which iterates **all** of
  `record.RecordFields` with no field-type filter. Hidden fields are included, and `RecordField(Field)` sets `Field`,
  `FieldId` and `Alias`, so fields the mapper creates render with their caption too. Confirm on the running site anyway.
- **Does re-running one Approve-stage workflow re-fire the whole stage?** No. `RetryWorkflowController.Retry` is
  `POST …/form/{formId}/record/{recordId}/workflow/{workflowId}/retry` — a single workflow id, resolved and run alone.

## Form simplified (2026-09-08, after the first walkthrough)

Fourteen fields felt like a lot for "book me a Meet", so the form was cut to nine visible ones. Removed:
*Contact email*, *Expected attendees*, *Anything else* and the *Data Consent* checkbox; *Provisioning state* went too,
since state moved to the EF table and the field was dead.

Dropping the first three needed **no** code beyond deleting them: all three were optional in the mapper, and the Apps
Script's `buildDescription_` never read `contactEmail`, `expectedAttendees` or `notes`. They are gone from
`MeetRequest` and the payload now.

*Meeting title* and *What is the meeting for?* were briefly merged into a single **Meeting description**, with the
Calendar title derived from its first line. That was reverted on request: **Meeting title** is back and required
(the Apps Script's `validateRequest_` needs a title of ≥3 characters anyway), and **Meeting description** is now
**optional** — most requests are "organise an online or hybrid meetup" and need no elaboration.

Removing the consent checkbox is a data-protection decision as much as a UI one: the form still collects a name and a
personal email, and that checkbox was the recorded lawful basis. Flagged for whoever owns GDPR for the community site;
a privacy notice should be reachable from the page.

Two process notes, both learned the hard way:

- **Rebuild the whole solution, not just the RCL, before testing.** `dotnet build src/UmbracoCommunity.MeetBooking`
  does not refresh `UmbracoCommunity.Web.UI/bin`, so the running site keeps the old assembly and you test yesterday's
  code. Symptom here was `form: Form field 'meetingTitle' is required but empty` from a build that predated the change.
- **When scripting the Forms designer, never target "the last `umb-input-with-alias`" without checking it is empty
  first** — the Add-field panel renders asynchronously, and a premature write renames whichever existing field is last
  in the DOM. It silently renamed *Co-host action* to *Meeting title*; caught before saving, discarded by reloading.

## Settled: automated co-hosts, no continuous chat (2026-09-10)

The design flipped twice in one afternoon and landed back where it started, which is worth recording because both
directions were live and working, and the reasoning is the useful part.

**Final shape.** `CONFERENCE_MODE=own-space`, `COHOST_MODE=api`, `presentRestriction=HOSTS_ONLY`, `CALENDAR_ID` on
the shareable "Community Meets" calendar.

**Why.** Automated co-hosts and continuous meeting chat are mutually exclusive: the co-host API only accepts a space
our app created, and continuous chat only appears when *Calendar* mints the conference. Chat was chased hard —
five falsified causes, a full measured grid — and then given up, because an automatic co-host is worth more to a
community meetup than persistent chat.

The settings hold each other up, which is the bit to not break:

- `HOSTS_ONLY` presenting is only safe because the requester is made a co-host automatically. Without the co-host,
  a room with no `community@` present has nobody who can share a screen.
- Conversely, if the co-host is ever turned off (`COHOST_MODE=off`), presenting must be relaxed in the same change.

**What `own-space` does *not* cost:** host controls. `spaces.patch` never cared who created the space — a read-back
of an own-space booking shows `moderation: ON` and `presentRestriction: HOSTS_ONLY` stored as sent. What it costs is
continuous chat and the Calendar Meet-settings *gear*, i.e. the UI for hand-tweaking. Since the co-host is added
automatically, the gear's main job is gone.

**Freed up by giving chat up:** `CALENDAR_ID`. A secondary calendar suppresses chat, but `own-space` rules chat out
anyway — so bookings can go back on a "Community Meets" calendar that is shareable with the community team, instead
of the host account's own agenda.

**The other configuration is still real, not theoretical**, and was live for an afternoon: `CONFERENCE_MODE=calendar`
+ `CALENDAR_ID=primary` + `COHOST_MODE=manual` + `presentRestriction=NO_RESTRICTION` gives continuous chat, the
Calendar gear, and a one-click manual co-host per approval. Switching is script properties only.

Also added in this round: a **`Community Meet: post to Slack`** workflow type, because Forms' built-in Slack
workflow takes only a webhook URL and always dumps every field — a dozen lines hiding the Meet link and the
co-host task. Ours posts one line, escapes requester input (a title containing `|` could otherwise forge a link in
the channel), and still posts on failure so a broken booking is not silent. Eight tests on the message alone.

One Forms 18 gotcha found writing it: `[Setting(..., View = "TextField")]` renders as *"The configured property
editor UI could not be found"*. Forms 18 resolves setting editors by property-editor UI alias
(`Umb.PropertyEditorUi.*`) and there is no `TextBox` among them — omit `View` and the default text input is used,
which is what Forms' own Slack workflow does.

## Co-host automated (2026-09-09) — Developer Preview granted

The account was accepted into the [Google Workspace Developer Preview Program](https://developers.google.com/workspace/preview),
so `POST /v2beta/spaces/{space}/members` is reachable and the co-host step no longer needs a human. This supersedes
*Co-host: manual, by decision* below, which stands as the record of why it was manual for a day.

Checked against the docs before changing anything: the call the script already made is the documented one
([spaces.members](https://developers.google.com/workspace/meet/api/guides/meeting-spaces-configuration) — `email` is
required for create, `role: COHOST`, scope `meetings.space.settings`, which the manifest already requests). Members
is still the *only* preview-gated thing we use: the v2beta `SpaceConfig` has no field the GA one lacks, and the
`[Developer Preview]` markers on `moderation` / `artifactConfig` / `attendanceReportGenerationType` in the generated
client references are stale — those shipped GA in Feb and Apr 2025, which is why our `v2/spaces.patch` already works.
Nothing in v2beta touches the continuous-chat gap, so that limitation is unchanged.

What changed:

- **`COHOST_MODE` gained a third value.** `api` (now the real default) calls the API and lets a failure fail the
  booking — the right behaviour while proving the preview out, because a silent fallback would hide a
  misconfiguration. `auto` makes the same call but degrades to manual instructions with `ok: true`, for later. `manual`
  is unchanged. An unrecognised value warns and is treated as `api` rather than silently skipping the step.
- **The create call is now idempotent.** `adoptExistingCohost_` pages the space's members and skips the create when
  the email is already a `COHOST`. This matters because a stateless retry reaches step 4 with `cohostAdded` unset
  after `adoptExistingEvent_` hands back an existing event — previously that would have re-POSTed, and the behaviour
  of the API on a duplicate is not documented. Listing first doesn't depend on knowing it. A resume also *retries* the
  step, so a booking that fell back to manual picks its co-host up once the API works.
- **`checkCohostApi()`** — a preflight that proves the preview without booking anything: `spaces.create` (GA, no
  event, no invitations), then list → create → delete against v2beta, then a diagnosis keyed to the status code (404 =
  project not enrolled or script pointed at the wrong Cloud project; 403 = missing scope; 400 = non-Google address).
  It leaks one inert scratch space per run — the API has no space delete.
- **`cohostError`** flows from the script through `MeetProvisioningState` into the entry's `provisioningError`, so a
  degraded co-host in `auto` mode is distinguishable from a deliberately manual one instead of living only in the
  execution log.

Fixed in passing: the guest-facing event description claimed *"Anyone with the link can join directly"*, which is
false under the default `accessType: 'TRUSTED'` — only the invited requester joins directly, everyone else knocks. It
now says what the configured access type actually does. The Apps Script README's test step 4 had the same error
("access type is Open").

### What the preview alone did not fix, and the actual cause (same day)

Enrolment made v2beta reachable — the old `404 "Method not found."` became `403 PERMISSION_DENIED` on `Member` — but
a real booking still failed at `cohost`. Three runs settled why, in this order:

1. `showGrantedScopes()` — both `meetings.space.settings` and `meetings.space.created` **granted**. Not a scope
   problem, which is where everyone's first guess goes. (The manifest listing a scope says nothing about the token;
   a scope added after the last consent is simply absent. Worth keeping that helper for the next 403.)
2. `checkCohostApi()` — **PASS**, on a space the script created via `spaces.create`, adding an *external* gmail as
   `COHOST`. That answers the design doc's risk #1: external addresses are fine, the co-host promise is not dead.
3. The decisive pairing: on the failed booking's Calendar-created space, `spaces.patch` **succeeded** (step
   `config` passed) and `members.create` on that same space with that same token returned 403. So the space is
   visible and writable; member management specifically is gated on **who created the space**.

`meetings.space.created` authorises member management on spaces the app created. `meetings.space.settings`
authorises *configuration* on another app's spaces — which is exactly how Google introduced it (Feb 2025, for
auto-artifacts on Calendar-created meetings) — and not members. Nothing documents the members restriction.

**The fix: own the space.** `testAttachOwnSpace()` confirmed Calendar accepts a space we created, attached via
`conferenceData.conferenceSolution` + `entryPoints` (the documented alternative to `createRequest`), and that the
co-host call then works on it. `provision_` was restructured around this: adopt an existing event first, else
create the space, record it in state, then insert the event with the space attached. Two things fell out of it:

- **An attached conference has `entryPoints` but no `conferenceId`.** `adoptExistingEvent_` read `conferenceId`,
  so a stateless retry would have recovered an event with no Meet link. It now reads the entry point's `uri` and
  falls back to `conferenceId` for bookings made before this change.
- **Calendar shows no Meet settings gear on an attached conference**, confirmed by A/B against an older booking.
  Host controls are still enforced (read back in full via `showLatestSpaceConfig()`), but there is no Calendar UI
  to hand-adjust them or the co-host list. Either/or with `createRequest`, which keeps the gear and 403s on
  co-hosts. Attaching wins because adding a co-host was the gear's main job and is now automatic.
- **Order matters.** Creating the space before adopting would mint a fresh space and then adopt an event pointing
  at the old one. Adoption runs first.

Google's caution against reusing conference data across events does not apply: every booking gets its own space.

**Continuous meeting chat — solved, and it reversed the design.** There are **two independent suppressors**,
either of which alone reads as a complete explanation, which is why five wrong causes went into this repo first:

1. **Attaching our own space** — no chat, on any calendar.
2. **A secondary calendar** — no chat, *including for an event created there by hand*, and moving an event onto
   one loses the chat as well.

The full measured grid:

| Conference created by | primary calendar | secondary calendar |
|---|---|---|
| hand, in the Calendar UI | chat ✓ | chat ✗ |
| Calendar, via `createRequest` | chat ✓ | chat ✗ |
| us, attached (`spaces.create`) | chat ✗ | chat ✗ |
| `createRequest`, then moved to secondary | — | chat ✗ |

**So the attach approach was undone the same day it landed**, because chat needs Calendar to mint the conference —
which is exactly the case where `spaces.members` 403s. Continuous chat and automated co-hosts are mutually
exclusive, full stop.

Sebastiaan chose chat, and the way *out* of the trade-off rather than through it: `presentRestriction` drops to
`NO_RESTRICTION`, removing the reason a co-host was mandatory (hosts-only presenting meant a room with no co-host
and no `community@` had nobody who could share a screen). With presenting open, no co-host is needed, so there is
no manual step to reintroduce. A host can still tighten it from the Host controls, and `moderation` stays `ON` so
those controls exist.

Shipped as configuration rather than deletion, since both paths are written and verified and this decision has
already flipped once:

- **`CONFERENCE_MODE`** = `calendar` (default: chat, the Calendar settings gear, host controls, no co-host API) or
  `own-space` (automated co-hosts, no chat, no gear).
- **`COHOST_MODE`** gains `off` and defaults to it. `api`/`auto` are downgraded to `manual` with a warning under
  `calendar`, where they would otherwise fail or nag on every single booking.
- **`CALENDAR_ID`** stays `primary`. The shareable "Community Meets" calendar is the price of chat — a real cost,
  recorded rather than glossed: bookings now sit on the host account's own agenda.

One bug this would have introduced, caught before it shipped: skipping the co-host step leaves neither
`cohostAdded` nor `cohostManual` set, and `MeetProvisioningState.IsComplete` required one of them — so a finished
booking would have looked unfinished forever and every re-run would have called Google again. There is now an
explicit `cohostSkipped` on both sides, with tests for it counting as complete, for *no* outcome still counting as
incomplete, and for surviving a JSON round-trip.

Also fixed: `buildDescription_` now derives every behavioural line from the config being applied instead of
hardcoding it. Those lines had been wrong twice — "anyone with the link can join directly" under
`accessType: TRUSTED`, and "you are co-host" in a mode that adds no co-host.

Retired: `testMoveToSecondary()` and `testLegacyConferenceOnPrimary()`, whose questions are closed for good.
`testAttachOwnSpace()`, `checkCohostApi()`, `showGrantedScopes()` and `showLatestSpaceConfig()` stay — each is
still the fastest way to answer a question that will come back.

For the next person: six explanations, five wrong, each one committed to this repo as fact. Every wrong one was an
API-side story; the variables nobody had varied were the calendar, and the interaction between the two
suppressors. When several explanations in one area all get falsified, the area is wrong — list what has *not* been
varied and vary that, rather than generating another theory in the same place.


Same category, asked at the same time: the two add-on switches (*Let contributors share add-on activities*,
*Allow third-party apps to collect audio and video*) are also not in `SpaceConfig`, so the script cannot touch
them. Both are Admin console settings — Media API access and add-on visibility respectively — org-wide, needing
Super Admin or *Manage Meet Settings*. Documented with paths in the Apps Script README.

**Still Sebastiaan's to do:** redeploy the script with the restructured `provision_`, run a real booking, and
confirm `cohostAdded: true` with `cohostAction` empty on the entry — plus a glance at whether continuous chat is
now present. Delete the orphan events the failed 403 runs left behind (`api` mode fails the booking after the
event exists, so nothing tracks them).

## Task 13, final verification (2026-09-08) — green

Record 120, `COHOST_MODE=manual`, against the redeployed Apps Script:

| Check | Result |
|---|---|
| Create Meet | **Completed** (no 404 — manual branch taken) |
| `meetLink` on the entry | `https://meet.google.com/tey-jkzj-fne` |
| `provisioningError` | empty |
| `cohostAction` on the entry | *"Open the event as the host account → gear icon → Co-hosts → add … → Save. Without this nobody can share a screen"* |
| Slack: Meet link posted | Completed |
| Stateless retry (state row deleted) | **same `eventId dtbu98pbaa9ra2enrd8io5rkr8`** — adopted, no second event |

Two things this settles that earlier runs did not:

- **A `COHOST_MODE` change needs no redeploy.** `config_()` reads script properties per execution; the switch from
  `api` to `manual` took effect on the next request against an untouched deployment.
- **`cohostAction` reaches the approver**, restoring what moving state into the EF table had hidden.

Note for anyone re-testing: the running app must actually be on the current build. One round of "cohostAction is
empty" was simply a process started before that code was compiled — check the process start time against the
assembly's, and if you inspect a .NET DLL for string literals use `strings -el` (UTF-16), not plain `strings`.

## Task 13, second real run (2026-09-08) — both bugs verified fixed

Re-ran end to end against the corrected Apps Script deployment. **Both fixes hold.**

- **Bug A (state too big for a Forms field):** approve now succeeds, the 598-character state lands in
  `MeetProvisioningStates`, and `meetLink` appears on the entry. No truncation error, no 500.
- **Bug B (duplicate events):** record `5474ca38-82e3-40e4-bd30-f4f8db3c4120` was approved, creating event
  `lufm5r8knvc1vbjinfi4rab7l0` / `neq-fcvx-tmf`. Its state row was then **deleted** to force a fully stateless retry —
  the exact condition that produced five duplicates earlier — and the retry came back with the **same** `eventId`.
  `adoptExistingEvent_` found the tagged event and adopted it.
- **Umbraco-side idempotency** (state present, complete): short-circuits with *"already has a complete Meet; nothing to
  do"* and never calls Google.
- **Per-workflow retry:** re-running Create Meet still fires only that workflow; Slack does not re-post.

**Two hours were lost to a deployment subtlety**, recorded in the bug doc: *Deploy → New deployment* creates a new
`/exec` URL and leaves the old deployment serving the old code at the URL the caller uses. Two rounds of "the fix
doesn't work" were really "the fix isn't being called". Config now points at the new deployment URL (in `appsettings.Local.json`, never the repo).

### Co-host: manual, by decision

> Superseded 2026-09-09 — the preview was granted; see *Co-host automated* above. Kept for the reasoning.

`spaces.members` (role `COHOST`) is Developer Preview only, `/v2beta/`, and the account is not enrolled — the call
returns `HTTP 404 "Method not found."`. There is no GA equivalent; the April 2025 GA release covered space
*configuration*, not members. Option A (manual co-host) was chosen over relaxing `presentRestriction`.

Because moving state off the entry also hid the script's co-host instructions from the approver, a **`cohostAction`**
hidden field was added: written whenever the co-host is left to a human, empty otherwise, so the task shows on the
entry and in the Slack post. Three tests cover it.

**Set `COHOST_MODE=manual`.** With `api` and no preview access the script throws at the co-host step, so
`cohostManual` is never set, `cohostAction` stays empty, and the workflow reports Failed with a raw 404 rather than a
clear instruction. `manual` gives Completed plus an actionable task.

## Task 13, real run (2026-09-08) — two bugs found, one fixed here

The first non-DryRun approval failed, and the failure was compound.

**Bug A — the write-back did not fit (fixed).** A Forms **Hidden** field has `FieldDataType.String`, stored in
`UFRecordDataString.Value` — `nvarchar(255)`. A real state object is ~600 characters, so the write threw
*"String or binary data would be truncated"*. `WriteAndSave` already caught the exception, but that was not enough:
the failed command runs on Umbraco's **ambient scope**, so the whole approve request 500'd and rolled back. The entry
stayed `Submitted` and the state was lost.

*Fix (this change).* Provisioning state moves into the package's own `MeetProvisioningStates` table — EF Core in
`Storage/`, migrated on `UmbracoApplicationStarted`, using `IDbContextFactory` so it runs on its own connection and
cannot poison the request. The workflow writes the store **first**, then the entry's `meetLink` /
`provisioningError`; a failure on the second is logged and swallowed. Every value written to a form field is now
truncated to 255. The `provisioningState` hidden field is gone from the contract.

Verified rather than assumed: the migration was scaffolded against SQLite (the design-time factory), which emitted
`type: "TEXT"` in the migration *and* `HasColumnType("TEXT")` in the designer and snapshot. Applied to SQL Server that
fails with error 1919 — *"Column 'RecordId' is of a type that is invalid for use as a key column in an index"* —
because the Guid key came out as `nvarchar(max)`. Stripping the provider types from all three files (which is why
NotFoundTracker's migrations carry none) gives `uniqueidentifier` / `nvarchar(max)` / `datetime2`. Confirmed by
applying the migration to a scratch SQL Server database and round-tripping a 514-character state.

**Bug B — the Apps Script is not idempotent (open, not fixed here).** Three approvals of the same entry produced
three different `eventId`s. `requestId` on `conferenceData.createRequest` deduplicates only the conference within a
single `events.insert`; it does not stop a second event. The script is *resumable* (given `state`) but not
*idempotent* (given only `recordId`), contrary to what this plan, the design doc, and the script's own comments say.
Bug A is what exposed it: with state never persisting, every retry arrived stateless.

Fixed in `Code.gs` by `adoptExistingEvent_`: events are tagged with `extendedProperties.private.umbracoRecordId` on
insert and looked up before inserting, so a stateless retry adopts the existing event. Verified — see the final
verification below.

**Cleanup owed:** three real Calendar events + Meet spaces on the community account —
`gg0rcoa72f4kq3cu0evqeiebeo` / `kmd-ojsz-zfj`, `05nrnunfko4okcn3nuk179fq3k` / `ipt-tkaz-ejm`,
`hjt4l6u32918navd77vhog86fc` / `bjk-gyad-ddg`.

**Still to do:** re-run the real end-to-end on the fixed build (entry 117 is still `Submitted`), and confirm the
idempotency check — re-run Create Meet and see the same `eventId` come back.

## Task 13, dry-run pass (2026-09-08, local, `DryRun: true`)

Form built in the backoffice (Task 10) and the full flow exercised end to end. Everything passed.

**The form.** All 15 aliases verified straight out of `UFForms.Definition`. Field settings persisted as intended:
`FieldType=email` on both email fields, `FieldType=number` on duration, 96 start-time prevalues, 56 timezone
prevalues with `Europe/Copenhagen` as the default, and `DefaultValue=true` on the checkbox (which does render it
ticked). Workflows landed on the right stages in the right order — Approve stage is `Create Meet` at sortOrder 0,
`Slack: Meet link posted` at 1 — and the stored `workflowTypeId` is `6f2c1b0a-7d0e-4c3a-9b3e-2e4c0a1d5f02`, matching
the `Id` set in `CreateMeetWorkflow`.

**Submit → approve.** A real submission through the rendered page on `/test-page` (not the API — the Forms API is off,
and going through the page also proves the Spam Guard field passes a genuine browser submit). Stored values confirm the
first spike's reading of the storage layer: `meetingDate` is a real `DateTime`, `recordMeeting` a real `bit`, and the
three hidden fields exist and are empty, so the mapper updates rather than creates them.

Workflow audit for the record, in order:

| Workflow | Stage | Result |
|---|---|---|
| Send template email to test@test.com | Submitted | **Failed** — no local SMTP sender; a pre-existing default workflow, unrelated |
| Slack: new request | Submitted | Completed |
| Create Meet | Approved | Completed |
| Slack: Meet link posted | Approved | Completed (186 ms after Create Meet) |

Write-back landed on the entry and is visible in the backoffice: `meetLink` = `https://meet.google.com/dry-run`,
`provisioningState` = the state JSON, `provisioningError` empty.

**Both open checks are now settled.**

- **Does re-running one Approve-stage workflow re-fire the stage? No — confirmed empirically.** The entry details has a
  *Workflow Audit* table with a per-workflow **Run Again** (and **Retry** on failed ones). Running *Create Meet* again
  produced exactly one new audit row; the audit tally went to `Create Meet x2, Slack: Meet link posted x1`. Slack did
  not re-post.
- **Does the second Slack workflow see the write-back?** The code path is confirmed and the workflow completed straight
  after Create Meet, but the message *body* can only be checked by a human in the channel — flagged for Seb.

**Still only provable in a non-DryRun run:** idempotency. The dry-run state has no `eventId`, so
`CreateMeetWorkflow`'s already-complete short-circuit (step 4) is never reached — the DryRun branch (step 5) returns
first. A re-run in dry mode just rewrites the placeholder. The "re-run shows the same eventId" check therefore needs
the real pass.

**Four more corrections to Task 10, folded into the README:**

1. The setting is **Moderation → "Enable post moderation"**, not "Manual approval".
2. Per-form data retention offers only *Retain submitted/approved records forever* toggles — there is no per-form day
   count in the UI, so the 30/90/30 figures have to come from config.
3. The Spam Guard field renders a **honeypot decoy** input (labelled "Enquiry reference" here). Anything automating a
   submission must leave it empty.
4. The date picker (Pikaday) renders **two inputs sharing one name** — a visible formatted one and a
   `.datepickerfieldshadow` hidden one — so both values post. Harmless for humans; worth knowing when scripting.

Also worth recording: `IgnoreWorkFlowsOnEdit` defaults to `true`, so the workflow persisting its own write-back via
`IRecordStorage.UpdateRecord` cannot re-trigger workflows. And `MinimumLeadTimeHours` from Task 2 was deliberately
dropped from the shipped `MeetBookingOptions` — lead time is enforced by the date picker's relative *Minimum date*
setting instead, which the README now says.

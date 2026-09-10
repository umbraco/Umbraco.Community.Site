/**
 * Community Meet Booking — Apps Script side.
 *
 * Runs as community@umbraco.com. Creates a Calendar event with a Google Meet on the
 * "Community Meets" calendar, configures the Meet space (hosts-only
 * presenting, ...), adds the requester as co-host and returns the ids so the caller
 * can store them.
 *
 * Entry points:
 *   doPost(e)           — JSON API for the Umbraco On-Approve workflow (shared secret)
 *   doGet(e)            — pre-filled HTML form for a human approver (APPROVER_DOMAIN accounts)
 *   testProvision()     — run from the editor: books a test meeting on CALENDAR_ID, logs the result
 *   cleanupTest()       — run from the editor: deletes what testProvision created
 *   showConfig()        — run from the editor: prints effective config, never the secret
 *   checkCohostApi()    — run from the editor: proves the Developer Preview co-host call works, without
 *                         creating a Calendar event or inviting anybody
 *   showGrantedScopes() — run from the editor: what the current authorisation actually grants, which is not
 *                         necessarily what appsscript.json asks for
 *   showLatestSpaceConfig() — run from the editor: same, for the most recent booking on the calendar. Use this
 *                         one from the Run button, which cannot pass arguments.
 *   showSpaceConfig(x)  — reads back a live space's config and members. x is a meeting code ("abc-defg-hij"), a
 *                         full space name, or a Meet url. Says what IS, not what we sent. Callable from code.
 *   testAttachOwnSpace()— run from the editor: EXPERIMENT. Can a space we created ourselves be attached to a
 *                         Calendar event? If yes, the co-host 403 goes away. Cleans up after itself.
 *
 * Script Properties (Project Settings → Script Properties):
 *   SHARED_SECRET       required for doPost. Long random string. Same value in Umbraco config.
 *   CALENDAR_ID         which calendar bookings land on. Defaults to "primary"; a secondary calendar such as
 *                       "Community Meets" is fine and is the nicer choice, since it can be shared with the
 *                       community team. (It used to matter: a secondary calendar suppresses continuous meeting
 *                       chat. CONFERENCE_MODE='own-space' already rules chat out, so it no longer costs anything.)
 *   APPROVER_DOMAIN     who may use the doGet form. Comma-separated list of domains, defaults to "umbraco.dk".
 *   SUMMARY_PREFIX      optional prefix for the event title, e.g. "[Umbraco Community] ". Default: none.
 *   SPACE_CONFIG        optional JSON overriding DEFAULT_SPACE_CONFIG below.
 *   TEST_COHOST_EMAIL   a Google account you control, used by testProvision().
 *
 * Written by the script itself, not configuration — cleanupTest() removes them:
 *   LAST_TEST_STATE     the state of the last testProvision() booking, so cleanupTest() can find the event.
 *   LAST_TEST_CALENDAR  which calendar that booking went on, so cleanupTest() deletes from the right one even if
 *                       CALENDAR_ID changed in between.
 *   CONFERENCE_MODE     "own-space" (default) creates the Meet space via spaces.create and attaches it to the
 *                       event, which is what lets the co-host API work. Costs continuous meeting chat and the
 *                       Calendar Meet-settings gear; host controls still apply in full.
 *                       "calendar" lets Calendar mint the Meet instead: continuous chat works (on the primary
 *                       calendar) and the gear is present, but co-hosts cannot be automated — spaces.members 403s
 *                       on a space we did not create.
 *                       Mutually exclusive, and the trade was decided in favour of automated co-hosts. See README.
 *   COHOST_MODE         "api" (default) adds the co-host via the v2beta members endpoint. Needs
 *                       CONFERENCE_MODE="own-space" and the Cloud project enrolled in the Workspace Developer
 *                       Preview Program. A failure fails the booking, rather than shipping a hosts-only meeting
 *                       that nobody present can share a screen in.
 *                       "auto" is "api" but degrades to manual instructions instead of failing the booking.
 *                       "manual" never calls the API; the approver adds the co-host in Calendar by hand.
 *                       "off" does not touch co-hosts at all — only sensible with presenting unrestricted.
 *                       "api"/"auto" are downgraded to "manual" with a warning under CONFERENCE_MODE="calendar".
 *
 * See README.md next to this file for deployment steps.
 */

// ---------------------------------------------------------------------------
// Configuration
// ---------------------------------------------------------------------------

var MEET_API = 'https://meet.googleapis.com/';

// The host controls every meeting starts with. Keys mirror the Meet REST API SpaceConfig.
// Override without a code change via the SPACE_CONFIG script property (JSON, same shape).
var DEFAULT_SPACE_CONFIG = {
  // TRUSTED: the invited attendee (the requester) joins directly; anyone else with the link has to ask to join,
  // and the host or a co-host admits them. Meet batches simultaneous knocks behind "View all" -> "Admit all", so a
  // crowd is one click, not one click each. Set accessType to 'OPEN' in the SPACE_CONFIG script property if a
  // particular meeting should let everyone straight in.
  //
  // Continuous meeting chat (the kind that persists into Google Chat) is GIVEN UP, deliberately, as of 2026-09-10.
  // It has two independent suppressors, either of which alone looks like a complete explanation — which is why
  // five wrong causes got written down before the grid was complete:
  //   1. attaching our own space (CONFERENCE_MODE='own-space') — no chat, on any calendar;
  //   2. a secondary calendar — no chat, even for an event created there by hand, and moving one there loses it.
  // Chat would therefore need CONFERENCE_MODE='calendar', which is exactly the case where the co-host API 403s.
  // An automatic co-host won, so chat is gone — and because (1) already rules chat out, CALENDAR_ID is free to be
  // the shareable "Community Meets" calendar again. Full grid in the README; measured, not theorised.
  //
  // Resolved 2026-09-09 by booking onto 'primary': chat works there. Moving an event from primary to a secondary
  // calendar loses it again, so there is no way to have both — CALENDAR_ID stays 'primary' and the shareable
  // separate calendar is the price. There is no SpaceConfig field for chat retention either way; the org-level
  // control is the Admin console (Apps > Google Workspace > Google Meet > Meet safety settings), per OU/group/user.
  //
  // chatRestriction below is a different thing entirely: who may chat during the call.
  accessType: 'TRUSTED',
  moderation: 'ON',                  // required for co-hosts and the restrictions below
  moderationRestrictions: {
    // HOSTS_ONLY, settled 2026-09-10. Only host and co-hosts can share their screen — safe to require, because
    // CONFERENCE_MODE='own-space' adds the requester as co-host automatically, so there is always someone in the
    // room who can present. (This was briefly NO_RESTRICTION while continuous meeting chat was being chased; chat
    // needs Calendar to mint the conference, which is exactly the case where the co-host API 403s. Chat lost.)
    presentRestriction: 'HOSTS_ONLY',
    chatRestriction: 'NO_RESTRICTION',
    reactionRestriction: 'NO_RESTRICTION',
    defaultJoinAsViewerType: 'OFF'         // attendees keep mic and camera
  },
  attendanceReportGenerationType: 'GENERATE_REPORT',
  artifactConfig: {
    // All three follow the form's checkboxes — applyPerMeetingOptions_ overrides every one of these per booking,
    // so the values here only matter if a caller sends no flags at all. They are OFF for that case on purpose: a
    // community meetup should not be recorded or written down unless someone asked.
    //
    // Transcription and Gemini notes are independent switches in the API (verified 2026-09-09: notes still run
    // with transcription off), so both are always set explicitly rather than left to default. One checkbox drives
    // the pair, because for an editor it is one decision.
    recordingConfig:     { autoRecordingGeneration: 'OFF' },
    transcriptionConfig: { autoTranscriptionGeneration: 'OFF' },
    smartNotesConfig:    { autoSmartNotesGeneration: 'OFF' }
  }
  // NOT available in the Meet REST API (as of 2026-09) — the SpaceConfig field list is exhaustive and has nothing
  // for any of these. Per meeting by hand, or defaulted org-wide in the Admin console by someone with Super Admin
  // or "Manage Meet Settings":
  //   - notes/transcript language      -> Meet uses the account's default Meeting Records language; set it once
  //                                      for the host account in Meet settings.
  //   - "Include captions" on a recording -> no API field, no known account default. Per-meeting click.
  //   - "Allow third-party apps to collect audio and video" -> Admin console > Apps > Google Workspace >
  //     Google Meet > Meet safety settings > Media API. Changes can take up to 24h.
  //   - "Let contributors share add-on activities" -> Admin console > Apps > Google Workspace > Settings for
  //     Google Meet > Meet video settings > add-on visibility.
};

function config_() {
  var p = PropertiesService.getScriptProperties();
  // A malformed SPACE_CONFIG must not take the endpoint down. JSON.parse throwing here escapes config_() before
  // doPost can build a response, so the caller gets Google's HTML error page and a useless
  // "transport: HTTP 200, non-JSON response" on the Forms entry. Capture it and let doPost report it properly.
  var spaceOverride = p.getProperty('SPACE_CONFIG');
  var spaceConfig = DEFAULT_SPACE_CONFIG;
  var spaceConfigError = null;
  if (spaceOverride) {
    try {
      spaceConfig = JSON.parse(spaceOverride);
    } catch (err) {
      spaceConfigError = 'SPACE_CONFIG script property is not valid JSON. It must be a whole config object of the ' +
        'same shape as DEFAULT_SPACE_CONFIG, e.g. {"accessType":"OPEN", ...} — not a bare value. Parse error: ' +
        err.message;
    }
  }

  return {
    sharedSecret: p.getProperty('SHARED_SECRET') || '',
    calendarId: p.getProperty('CALENDAR_ID') || 'primary',
    approverDomains: (p.getProperty('APPROVER_DOMAIN') || 'umbraco.dk').toLowerCase().split(',').map(function (d) { return d.trim(); }).filter(String),
    summaryPrefix: p.getProperty('SUMMARY_PREFIX') || '',
    spaceConfig: spaceConfig,
    spaceConfigError: spaceConfigError,
    testCohostEmail: p.getProperty('TEST_COHOST_EMAIL') || '',
    conferenceMode: conferenceMode_(p.getProperty('CONFERENCE_MODE')),
    cohostMode: cohostMode_(p.getProperty('COHOST_MODE'), conferenceMode_(p.getProperty('CONFERENCE_MODE')))
  };
}

var CONFERENCE_MODES = ['calendar', 'own-space'];

/**
 * How the Meet gets made. The two are mutually exclusive and the trade-off is real (see README):
 *
 *   'calendar'   Calendar mints the conference (conferenceData.createRequest). Continuous meeting chat works, the
 *                Calendar Meet-settings gear is present, spaces.patch still applies all host controls — but
 *                spaces.members returns 403 because the space is not ours, so co-hosts cannot be automated.
 *   'own-space'  We create the space with spaces.create and attach it. Co-hosts are automated, but continuous
 *                meeting chat never appears and Calendar shows no settings gear.
 *
 * Default 'own-space', settled 2026-09-10: an automatically added co-host is worth more than continuous chat.
 * Continuous chat was the only reason to prefer 'calendar', and it cannot coexist with automated co-hosts.
 */
function conferenceMode_(raw) {
  var mode = String(raw || 'own-space').toLowerCase().trim();
  if (CONFERENCE_MODES.indexOf(mode) !== -1) return mode;
  console.warn('CONFERENCE_MODE "%s" is not one of %s — treating it as "own-space".', String(raw), CONFERENCE_MODES.join('/'));
  return 'own-space';
}

var COHOST_MODES = ['off', 'api', 'auto', 'manual'];

/**
 * What to do about co-hosts. Default 'api': the requester is made a co-host automatically, which is what makes
 * presentRestriction HOSTS_ONLY safe. A failure fails the booking rather than shipping a meeting nobody can
 * present in — switch to 'auto' if you would rather degrade to instructions than lose the booking.
 *
 * 'api' and 'auto' need CONFERENCE_MODE='own-space' — on a Calendar-minted conference the members call always
 * 403s, so 'api' would fail every booking and 'auto' would nag on every one. Both are downgraded with a warning
 * rather than being allowed to do that.
 */
function cohostMode_(raw, conferenceMode) {
  var mode = String(raw || 'api').toLowerCase().trim();
  if (COHOST_MODES.indexOf(mode) === -1) {
    console.warn('COHOST_MODE "%s" is not one of %s — treating it as "api".', String(raw), COHOST_MODES.join('/'));
    mode = 'api';
  }
  if (conferenceMode === 'calendar' && (mode === 'api' || mode === 'auto')) {
    console.warn('COHOST_MODE="%s" needs CONFERENCE_MODE="own-space" — spaces.members cannot touch a ' +
      'Calendar-minted conference. Treating it as "manual" so the booking still succeeds and the approver is told.', mode);
    return 'manual';
  }
  return mode;
}

// ---------------------------------------------------------------------------
// Web app: JSON API for Umbraco (On Approve workflow)
// ---------------------------------------------------------------------------

/**
 * POST body (JSON):
 * {
 *   "token":        "<SHARED_SECRET>",
 *   "recordId":     "forms record guid — used as the idempotency key",
 *   "title":        "Umbraco Copenhagen meetup",
 *   "description":  "purpose text from the form",
 *   "requesterName":"Jane Doe",
 *   "cohostEmail":  "jane@gmail.com",           // must be a Google account
 *   "startLocal":   "2026-09-17T19:00:00",      // wall-clock in timeZone, no offset
 *   "endLocal":     "2026-09-17T20:00:00",      // optional if durationMinutes given
 *   "durationMinutes": 60,
 *   "timeZone":     "Europe/London",            // IANA
 *   "recordMeeting": false,                     // optional, default FALSE. true → auto-recording ON
 *   "transcribeMeeting": false,                 // optional, default FALSE. true → transcript AND Gemini notes ON
 *   "spaceConfig":  { ...optional override of the defaults... },
 *   "state":        { ...previous partial result, for retries... }
 * }
 *
 * Response (always HTTP 200 — Apps Script web apps can't set status codes; check "ok"):
 * { "ok": true, "state": { eventId, meetingCode, meetUri, spaceName, configured, cohostAdded, htmlLink } }
 * { "ok": false, "error": "...", "step": "cohost", "state": { ...whatever succeeded... } }
 */
function doPost(e) {
  var cfg = config_();
  var req;
  try {
    req = JSON.parse(e && e.postData && e.postData.contents || '{}');
  } catch (err) {
    return json_({ ok: false, error: 'Body is not valid JSON' });
  }

  if (!cfg.sharedSecret) return json_({ ok: false, error: 'SHARED_SECRET script property is not set' });
  if (!req.token || !constantTimeEquals_(req.token, cfg.sharedSecret)) return json_({ ok: false, error: 'Unauthorised' });
  if (cfg.spaceConfigError) return json_({ ok: false, step: 'config', error: cfg.spaceConfigError });

  var problems = validateRequest_(req);
  if (problems.length) return json_({ ok: false, error: 'Invalid request: ' + problems.join('; ') });

  // Serialise concurrent calls (two approvers, or a retry racing a slow first attempt) so the same recordId
  // can't be provisioned twice in parallel. This lock is also what makes the list-then-insert in
  // adoptExistingEvent_ safe: without it two callers could both look, both miss, and both insert.
  var lock = LockService.getScriptLock();
  if (!lock.tryLock(30000)) return json_({ ok: false, step: 'lock', error: 'Another provisioning call is in progress; retry shortly' });
  try {
    return json_(provision_(req, req.state || {}, cfg));
  } finally {
    lock.releaseLock();
  }
}

// ---------------------------------------------------------------------------
// Web app: human approver form (deploy separately with access = domain users)
// ---------------------------------------------------------------------------

/**
 * GET with query params pre-fills the form. The Slack message links here.
 * e.g. ?recordId=...&title=...&cohostEmail=...&startLocal=2026-09-17T19:00:00&durationMinutes=60&timeZone=Europe/London
 */
function doGet(e) {
  var cfg = config_();
  var who = Session.getActiveUser().getEmail() || '';
  var allowed = isApprover_(who, cfg);

  var t = HtmlService.createTemplateFromFile('Form');
  t.allowed = allowed;
  t.who = who;
  t.approverDomain = cfg.approverDomains.join(' / ');
  t.p = e && e.parameter ? e.parameter : {};
  t.spaceConfigJson = JSON.stringify(cfg.spaceConfig, null, 2);
  return t.evaluate().setTitle('Create community Meet');   // default X-Frame-Options already blocks framing
}

/** Called from the form via google.script.run. Re-checks the caller's domain server-side. */
function provisionFromForm(form) {
  var cfg = config_();
  var who = Session.getActiveUser().getEmail() || '';
  if (!isApprover_(who, cfg)) {
    return { ok: false, error: 'Only ' + cfg.approverDomains.join(' / ') + ' accounts may create meetings (you are ' + (who || 'anonymous') + ')' };
  }
  var req = {
    recordId: form.recordId || ('manual-' + Utilities.getUuid()),
    title: form.title,
    description: form.description || '',
    requesterName: form.requesterName || '',
    cohostEmail: form.cohostEmail,
    startLocal: form.startLocal,
    durationMinutes: Number(form.durationMinutes || 60),
    timeZone: form.timeZone,
    recordMeeting: form.recordMeeting === 'on' || form.recordMeeting === 'true',
    transcribeMeeting: form.transcribeMeeting === 'on' || form.transcribeMeeting === 'true'
  };
  var problems = validateRequest_(req);
  if (problems.length) return { ok: false, error: 'Invalid request: ' + problems.join('; ') };
  var result = provision_(req, {}, cfg);
  result.approvedBy = who;
  return result;
}

// ---------------------------------------------------------------------------
// The actual work — idempotent, resumable
// ---------------------------------------------------------------------------

/**
 * Finds an event this recordId already created and adopts it into state, so a retry that arrives without state
 * resumes instead of creating a duplicate.
 *
 * This is the real idempotency guard. Calendar's conferenceData.createRequest.requestId is not one: it dedupes only
 * the conference attached within a single events.insert, so every insert makes a new event no matter what it says.
 * Relies on doPost's script lock to stop two callers both looking, both missing and both inserting.
 */
function adoptExistingEvent_(req, state, cfg) {
  var found = Calendar.Events.list(cfg.calendarId, {
    privateExtendedProperty: 'umbracoRecordId=' + recordKey_(req.recordId),
    showDeleted: false,
    maxResults: 2
  });

  // Ignore cancelled events, so a booking that was deliberately deleted can be re-created.
  var items = ((found && found.items) || []).filter(function (e) { return e.status !== 'cancelled'; });
  if (!items.length) return;

  var event = items[0];
  state.eventId = event.id;
  state.htmlLink = event.htmlLink;

  readConferenceInto_(event, state);
  console.log('Adopted existing event %s for record %s instead of creating a second one', event.id, String(req.recordId));
}

function provision_(req, state, cfg) {
  state = state || {};
  var step = 'start';
  try {
    // 1. Adopt an event this recordId already created, if there is one.
    //    This is the idempotency guard — see adoptExistingEvent_. Must come before the space is created, or a
    //    retry would mint a fresh space and then adopt an event pointing at the old one.
    step = 'calendar';
    if (!state.eventId) adoptExistingEvent_(req, state, cfg);

    // 2. In 'own-space' mode, create the Meet space OURSELVES so the co-host API will accept it.
    //
    //    Why that is even a choice (established 2026-09-09): spaces.members only works on a space the calling app
    //    created — on a Calendar-minted conference it 403s even though spaces.patch on the same space with the
    //    same token succeeds. Attaching our own space fixes co-hosts but suppresses continuous meeting chat. See
    //    conferenceMode_ for the trade-off; 'calendar' is the default because presenting is unrestricted.
    //
    //    The space is recorded in state before the event is inserted, so a retry after a failed insert reuses it
    //    instead of leaking another.
    if (cfg.conferenceMode === 'own-space' && !state.eventId && !state.spaceName) {
      step = 'space';
      var space = meet_('POST', 'v2/spaces', {});
      state.spaceName = space.name;
      state.meetUri = space.meetingUri;
      state.meetingCode = codeFromMeetUri_(space.meetingUri) || space.meetingCode;
    }

    step = 'calendar';
    if (!state.eventId) {
      var times = resolveTimes_(req);
      var event = Calendar.Events.insert({
        summary: cfg.summaryPrefix + req.title,
        description: buildDescription_(req, req.spaceConfig || cfg.spaceConfig || {}, cfg.cohostMode),
        start: { dateTime: times.startLocal, timeZone: req.timeZone },
        end:   { dateTime: times.endLocal,   timeZone: req.timeZone },
        attendees: [{ email: req.cohostEmail }],
        guestsCanInviteOthers: true,
        guestsCanModify: false,
        conferenceData: state.meetUri
          // 'own-space': hand Calendar the finished space. conferenceSolution + entryPoints is the documented
          // alternative to createRequest.
          ? {
              conferenceSolution: { key: { type: 'hangoutsMeet' }, name: 'Google Meet' },
              entryPoints: [{ entryPointType: 'video', uri: state.meetUri, label: state.meetingCode }]
            }
          // 'calendar': let Calendar mint it. requestId is NOT an idempotency key for the insert — it only dedupes
          // the conference within one events.insert. adoptExistingEvent_ is what stops duplicates.
          : {
              createRequest: { requestId: String(req.recordId), conferenceSolutionKey: { type: 'hangoutsMeet' } }
            },
        // The idempotency key. Private = only on the organiser's copy, which is the account that searches.
        extendedProperties: {
          private: { umbracoRecordId: recordKey_(req.recordId) }
        }
      }, cfg.calendarId, { conferenceDataVersion: 1, sendUpdates: 'all' });

      state.eventId = event.id;
      state.htmlLink = event.htmlLink;

      var wanted = state.meetUri;
      if (!readConferenceInto_(event, state)) {
        // createRequest is asynchronous and documented as possibly 'pending' on the insert response, in which case
        // there are no entryPoints yet. Re-read once before giving up, so a slow conference is not a failed booking.
        Utilities.sleep(2000);
        readConferenceInto_(Calendar.Events.get(cfg.calendarId, event.id), state);
      }
      if (!state.meetUri) {
        throw new Error('Event created but no Meet attached. conferenceData=' + JSON.stringify(event.conferenceData));
      }
      // In 'own-space' mode Calendar silently dropping our conferenceData would leave an event on a different Meet
      // than the space we configured and co-hosted. Catch that rather than ship a mismatch.
      if (wanted && state.meetUri !== wanted) {
        throw new Error('Calendar did not keep the attached Meet. Wanted ' + wanted + ', event has ' + state.meetUri);
      }
    }

    // 3. Resolve the space. In 'calendar' mode this is the only way we learn it; the meeting code is accepted as
    //    an alias for spaces.get. (The members endpoints are not so forgiving — they need the canonical name.)
    step = 'space';
    if (!state.spaceName) {
      state.spaceName = meet_('GET', 'v2/spaces/' + state.meetingCode).name;
    }

    // 4. Host controls.
    step = 'config';
    if (!state.configured) {
      var spaceConfig = applyPerMeetingOptions_(req, req.spaceConfig || cfg.spaceConfig);
      var mask = fieldMask_(spaceConfig, 'config');
      meet_('PATCH', 'v2/' + state.spaceName + '?updateMask=' + encodeURIComponent(mask), { config: spaceConfig });
      state.configured = true;
    }

    // 5. Co-host. Developer Preview endpoint (spaces.members) — see README.
    //    manual skips it and hands the job to the approver; auto tries the call and hands over only if it fails.
    step = 'cohost';
    if (!state.cohostAdded) {
      var effectiveSpaceConfig = req.spaceConfig || cfg.spaceConfig;
      if (cfg.cohostMode === 'off') {
        // Nothing to do and nothing to nag about: presenting is unrestricted, so no co-host is required. Recorded
        // explicitly because the Umbraco side treats a booking with no co-host outcome at all as unfinished.
        state.cohostSkipped = true;
      } else if (cfg.cohostMode === 'manual') {
        markCohostManual_(state, req, null, effectiveSpaceConfig);
      } else {
        try {
          addCohost_(req, state);
        } catch (err) {
          // "api" is the mode you run while proving the preview works, so let the booking fail and say why.
          if (cfg.cohostMode !== 'auto') throw err;
          console.error('Co-host API failed; COHOST_MODE=auto so falling back to manual. ' + err);
          markCohostManual_(state, req, err, effectiveSpaceConfig);
        }
      }
    }

    return { ok: true, state: state };
  } catch (err) {
    console.error('provision failed at ' + step + ': ' + err);
    return { ok: false, step: step, error: String(err && err.message || err), state: state };
  }
}

// ---------------------------------------------------------------------------
// Co-host (Developer Preview: spaces.members)
// ---------------------------------------------------------------------------

/**
 * Makes req.cohostEmail a co-host of the space, and clears any manual hand-off a previous attempt left behind.
 *
 * Checks the existing members first rather than relying on what the API does with a duplicate: a retry that arrives
 * without state (or one that adopted an already-provisioned event) reaches this with state.cohostAdded unset, and a
 * second POST for the same email must not be what fails the booking.
 */
function addCohost_(req, state) {
  if (!adoptExistingCohost_(req, state)) {
    meet_('POST', 'v2beta/' + state.spaceName + '/members', { email: req.cohostEmail, role: 'COHOST' });
  }
  state.cohostAdded = true;
  state.cohostManual = false;
  delete state.cohostSkipped;
  delete state.cohostInstructions;
  delete state.cohostError;
}

/** True when the email is already a co-host of the space. Pages, because pageSize maxes out at 100. */
function adoptExistingCohost_(req, state) {
  var wanted = String(req.cohostEmail).toLowerCase();
  var pageToken = '';
  do {
    var res = meet_('GET', 'v2beta/' + state.spaceName + '/members?pageSize=100' +
      (pageToken ? '&pageToken=' + encodeURIComponent(pageToken) : ''));
    var members = res.members || [];
    for (var i = 0; i < members.length; i++) {
      if (String(members[i].email || '').toLowerCase() === wanted && members[i].role === 'COHOST') {
        console.log('%s is already a co-host of %s; skipping the create call', req.cohostEmail, state.spaceName);
        return true;
      }
    }
    pageToken = res.nextPageToken || '';
  } while (pageToken);
  return false;
}

/**
 * Records the co-host as the approver's job. err is the API failure that caused it, or null in manual mode.
 *
 * The "why it matters" half is derived from the config, not fixed: it used to say "without this nobody can share a
 * screen", which stopped being true the moment presentRestriction became NO_RESTRICTION. This string lands on the
 * Forms entry and in the Slack post, so a stale reason is a stale instruction to a human.
 */
function markCohostManual_(state, req, err, spaceCfg) {
  var cfg = spaceCfg || {};
  var restrictions = cfg.moderationRestrictions || {};
  var why;
  if (restrictions.presentRestriction === 'HOSTS_ONLY') {
    why = 'Without this nobody can share a screen.';
  } else if (cfg.accessType && cfg.accessType !== 'OPEN') {
    // TRUSTED/RESTRICTED: uninvited attendees knock, and only a host or co-host can let them in.
    why = 'Uninvited guests must knock, and only a host or co-host can admit them.';
  } else {
    why = 'Optional here — it just lets them mute, remove, or end the call.';
  }
  var hostAccount = 'the host account';
  try {
    hostAccount = Session.getEffectiveUser().getEmail() || hostAccount;
  } catch (err) {
    // Never worth failing a booking over a label.
  }

  state.cohostManual = true;
  state.cohostInstructions = 'Open the event as ' + hostAccount +
    ' → gear icon next to the Meet link → Co-hosts → add ' + req.cohostEmail + ' → Save. ' + why;
  if (state.cohostInstructions.length > 255) {
    // The Umbraco side stores this in nvarchar(255) and truncates. Warn rather than quietly ship a clipped reason.
    console.warn('cohostInstructions is %s characters and will be truncated on the Forms entry.',
      state.cohostInstructions.length);
  }
  if (err) state.cohostError = String(err && err.message || err);
}

// ---------------------------------------------------------------------------
// Editor-run helpers: test and clean up
// ---------------------------------------------------------------------------

function testProvision() {
  var cfg = config_();
  if (!cfg.testCohostEmail) throw new Error('Set the TEST_COHOST_EMAIL script property to a Google account you control first.');

  var start = new Date(Date.now() + 2 * 24 * 3600 * 1000);
  start.setMinutes(0, 0, 0);
  var req = {
    recordId: 'test-' + Utilities.getUuid(),
    title: 'TEST — delete me',
    description: 'Created by testProvision() in the community Meet booking script.',
    requesterName: 'Test Requester',
    cohostEmail: cfg.testCohostEmail,
    startLocal: Utilities.formatDate(start, 'Europe/Copenhagen', "yyyy-MM-dd'T'HH:mm:ss"),
    durationMinutes: 30,
    timeZone: 'Europe/Copenhagen',
    recordMeeting: false,              // flip to true to test the recording toggle the other way
    transcribeMeeting: false           // and this one for the transcript + Gemini notes
  };

  var result = provision_(req, {}, cfg);
  console.log(JSON.stringify(result, null, 2));
  // Remember which calendar it landed on, so cleanupTest deletes from the right one.
  PropertiesService.getScriptProperties().setProperty('LAST_TEST_STATE', JSON.stringify(result.state || {}));
  PropertiesService.getScriptProperties().setProperty('LAST_TEST_CALENDAR', cfg.calendarId);

  if (result.ok) {
    console.log('OK on calendar "' + cfg.calendarId + '". Meet: ' + result.state.meetUri + '\nEvent: ' + result.state.htmlLink +
      (result.state.cohostAdded ? '\nCo-host added via the API.' : '') +
      (result.state.cohostManual ? '\nCo-host NOT added — do it by hand: ' + result.state.cohostInstructions +
        (result.state.cohostError ? '\nThe API said: ' + result.state.cohostError : '') : '') +
      '\nCheck the invite arrived at ' + cfg.testCohostEmail + ' and that they join as co-host.' +
      '\nOpen the event and look at the video-call options for "Continuous meeting chat". Then run cleanupTest().');
  } else {
    console.log('FAILED at step "' + result.step + '". Partial state saved; run testResume() after fixing, or cleanupTest() to delete the event.');
  }
  return result;
}

/** Re-runs the last failed test from where it stopped — exercises the resume path. */
function testResume() {
  var cfg = config_();
  var state = JSON.parse(PropertiesService.getScriptProperties().getProperty('LAST_TEST_STATE') || '{}');
  if (!state.eventId) throw new Error('No partial test state to resume.');
  var result = provision_({ recordId: 'resume', title: 'TEST', cohostEmail: cfg.testCohostEmail,
    startLocal: '2000-01-01T00:00:00', durationMinutes: 30, timeZone: 'Europe/Copenhagen' }, state, cfg);
  console.log(JSON.stringify(result, null, 2));
  PropertiesService.getScriptProperties().setProperty('LAST_TEST_STATE', JSON.stringify(result.state || {}));
  return result;
}

function cleanupTest() {
  var cfg = config_();
  var p = PropertiesService.getScriptProperties();
  var state = JSON.parse(p.getProperty('LAST_TEST_STATE') || '{}');
  // Delete from whichever calendar the last test actually used — CALENDAR_ID can change between a test and its
  // cleanup, and then a stale event is left behind on a calendar nobody looks at.
  var calendarId = p.getProperty('LAST_TEST_CALENDAR') || cfg.calendarId;
  if (state.eventId) {
    // Deleting the event by hand first is the normal case, not an error — but if the remove throws and we bail,
    // LAST_TEST_* are never cleared and linger in Script Properties forever. Clear them either way.
    try {
      Calendar.Events.remove(calendarId, state.eventId, { sendUpdates: 'all' });
      console.log('Deleted event %s from "%s" (Meet %s).', state.eventId, calendarId, state.meetUri || '?');
    } catch (err) {
      console.log('Could not delete event %s from "%s" — most likely already gone. Clearing the saved state ' +
        'anyway. Google said: %s', state.eventId, calendarId, String(err && err.message || err));
    }
  } else {
    console.log('Nothing to clean up.');
  }
  p.deleteProperty('LAST_TEST_STATE');
  p.deleteProperty('LAST_TEST_CALENDAR');
  console.log('Cleared LAST_TEST_STATE and LAST_TEST_CALENDAR.');
}

/**
 * Proves the Developer Preview co-host call works, before any real booking depends on it.
 *
 * Creates a bare Meet space (spaces.create is GA — no Calendar event, no invitations, no attendees), then runs the
 * three v2beta member calls the booking flow needs against it and removes the member again. The space itself is left
 * behind: the API has no delete for one. It has no conference, no calendar entry and nobody has the link, so it is
 * inert — but that is why this creates one space per run rather than something you should run in a loop.
 */
function checkCohostApi() {
  var cfg = config_();
  if (!cfg.testCohostEmail) throw new Error('Set the TEST_COHOST_EMAIL script property to a Google account you control first.');

  var space = meet_('POST', 'v2/spaces', {});
  console.log('Scratch space: %s (%s)', space.name, (space.meetingUri || '?'));

  var step = 'list';
  try {
    meet_('GET', 'v2beta/' + space.name + '/members?pageSize=1');
    step = 'create';
    var member = meet_('POST', 'v2beta/' + space.name + '/members', { email: cfg.testCohostEmail, role: 'COHOST' });
    console.log('Added %s as %s → %s', cfg.testCohostEmail, member.role, member.name);
    step = 'delete';
    meet_('DELETE', 'v2beta/' + member.name);
    console.log('PASS — the co-host API works. COHOST_MODE=%s will add co-hosts automatically.', cfg.cohostMode);
    return true;
  } catch (err) {
    var msg = String(err && err.message || err);
    console.error('FAIL at the "%s" call: %s', step, msg);
    if (msg.indexOf('HTTP 404') !== -1) {
      console.error('HTTP 404 "Method not found" means v2beta is not reachable for this Cloud project: either it is ' +
        'not enrolled in the Workspace Developer Preview Program, or Apps Script is still pointed at its own default ' +
        'project. Check Project Settings → Google Cloud Platform (GCP) Project.');
    } else if (msg.indexOf('HTTP 403') !== -1) {
      console.error('HTTP 403 PERMISSION_DENIED on Member is ambiguous — Google returns it both for "not allowed" and ' +
        '"no such thing". Check the grant first: run showGrantedScopes() and look for meetings.space.settings. ' +
        'If it is granted and this still fails, note that THIS check uses a space the script created itself, so a ' +
        'failure here is not about the space\'s origin; a pass here while a real booking fails means member ' +
        'management does not reach spaces created by Calendar\'s events.insert.');
      showGrantedScopes();
    } else if (msg.indexOf('HTTP 400') !== -1) {
      console.error('HTTP 400 on "create" is what a non-Google address looks like. Point TEST_COHOST_EMAIL at a real ' +
        'Google account.');
    }
    return false;
  }
}

/**
 * Prints the scopes the current authorisation actually granted, and flags any the manifest asks for but the grant
 * is missing. Editing appsscript.json does not re-prompt on its own, so a scope added after the last consent is
 * simply absent from the token — which surfaces as a 403 on the call that needed it, not as anything obvious.
 *
 * Asks Google's tokeninfo endpoint about the script's own token. The token itself is never logged.
 */
function showGrantedScopes() {
  var res = UrlFetchApp.fetch('https://oauth2.googleapis.com/tokeninfo?access_token=' +
    encodeURIComponent(ScriptApp.getOAuthToken()), { muteHttpExceptions: true });
  if (res.getResponseCode() >= 300) throw new Error('tokeninfo → HTTP ' + res.getResponseCode() + ': ' + res.getContentText());

  var granted = String(JSON.parse(res.getContentText()).scope || '').split(/\s+/).filter(String);
  console.log('Granted scopes (%s):\n  %s', granted.length, granted.join('\n  '));

  // The two the co-host step depends on. meetings.space.created covers spaces this script created; the booking
  // flow's spaces come from Calendar's events.insert instead, which is what meetings.space.settings is for.
  ['https://www.googleapis.com/auth/meetings.space.settings',
   'https://www.googleapis.com/auth/meetings.space.created'].forEach(function (scope) {
    console.log('%s  %s', granted.indexOf(scope) !== -1 ? 'GRANTED ' : 'MISSING!', scope);
  });
  if (granted.indexOf('https://www.googleapis.com/auth/meetings.space.settings') === -1) {
    console.error('meetings.space.settings is not in the grant. Revoke the script under ' +
      'myaccount.google.com → Data & privacy → Third-party access, then run showConfig() and accept again.');
  }
  return granted;
}

/**
 * EXPERIMENT, not part of the booking flow. Establishes whether we can dodge the co-host 403.
 *
 * Established 2026-09-09: members.create succeeds on a space this script created (checkCohostApi passes, external
 * gmail and all) and returns 403 PERMISSION_DENIED on the space behind a conference that Calendar's events.insert
 * created — even though spaces.patch on that same space, with the same token, succeeds. So member management is
 * gated on the space's origin, not on the grant.
 *
 * The dodge, if Calendar allows it: create the space ourselves via spaces.create (GA), then attach it to the event
 * with conferenceSolution + entryPoints instead of letting createRequest mint one. The docs say "either
 * conferenceSolution and at least one entryPoint, or createRequest is required", so the shape is legal; whether
 * Calendar accepts it for hangoutsMeet specifically is undocumented. conferenceData.signature is read-only, which
 * may or may not matter — signature verification is described for *copied* conference data.
 *
 * (Google warns against reusing conference data across events. That is about sharing one conference between many
 * events; this creates a fresh space per event, so it does not apply.)
 *
 * Logs what the event came back with, then tries the co-host call, then deletes the event.
 */
function testAttachOwnSpace() {
  var cfg = config_();
  if (!cfg.testCohostEmail) throw new Error('Set the TEST_COHOST_EMAIL script property to a Google account you control first.');

  var space = meet_('POST', 'v2/spaces', {});
  console.log('1. Created space %s → %s', space.name, space.meetingUri);

  var start = new Date(Date.now() + 3 * 24 * 3600 * 1000);
  start.setMinutes(0, 0, 0);
  var startLocal = Utilities.formatDate(start, 'Europe/Copenhagen', "yyyy-MM-dd'T'HH:mm:ss");
  var end = new Date(start.getTime() + 30 * 60 * 1000);
  var endLocal = Utilities.formatDate(end, 'Europe/Copenhagen', "yyyy-MM-dd'T'HH:mm:ss");

  var eventId = null;
  try {
    var event = Calendar.Events.insert({
      summary: 'TEST attach-own-space — delete me',
      description: 'Experiment: Meet space created via the Meet API, attached rather than created by Calendar.',
      start: { dateTime: startLocal, timeZone: 'Europe/Copenhagen' },
      end:   { dateTime: endLocal,   timeZone: 'Europe/Copenhagen' },
      conferenceData: {
        conferenceSolution: { key: { type: 'hangoutsMeet' }, name: 'Google Meet' },
        entryPoints: [{ entryPointType: 'video', uri: space.meetingUri, label: space.meetingCode }]
      }
    // No attendees and sendUpdates 'none': this must not email anybody.
    }, cfg.calendarId, { conferenceDataVersion: 1, sendUpdates: 'none' });
    eventId = event.id;

    var attached = event.conferenceData || {};
    var uri = ((attached.entryPoints || [])[0] || {}).uri || null;
    console.log('2. Event %s created. conferenceData came back as:\n%s', eventId, JSON.stringify(attached, null, 2));

    if (uri !== space.meetingUri) {
      console.error('VERDICT: NO. Calendar did not keep our space (event uri = %s, ours = %s). The attach dodge is ' +
        'closed; stay on COHOST_MODE=auto and add co-hosts by hand.', String(uri), space.meetingUri);
      return false;
    }
    console.log('3. Calendar KEPT our space. Now the call that 403s on a Calendar-created space...');
    var member = meet_('POST', 'v2beta/' + space.name + '/members', { email: cfg.testCohostEmail, role: 'COHOST' });
    console.log('4. Co-host added → %s', member.name);
    console.log('VERDICT: YES. Creating the space ourselves and attaching it makes the co-host call work. Worth ' +
      'rewriting provision_ step 1 around this. Open the event and check whether continuous meeting chat is ' +
      'present too — that is a genuinely new experiment on that problem, not a re-theorise.');
    return true;
  } catch (err) {
    console.error('VERDICT: NO — %s', String(err && err.message || err));
    console.error('If that came from events.insert, Calendar rejects an attached hangoutsMeet conference and the ' +
      'dodge is closed. If it came from the members call, something else is going on.');
    return false;
  } finally {
    if (eventId) {
      Calendar.Events.remove(cfg.calendarId, eventId, { sendUpdates: 'none' });
      console.log('Cleaned up event %s. The scratch space stays — the API has no space delete.', eventId);
    }
  }
}

/**
 * Reads a live space back and prints its config and members. spaces.patch returning 200 is not proof the settings
 * stuck, and Meet's own UI only shows host controls to someone inside the call — so this is how to check a booking
 * without joining it.
 *
 * Accepts a meeting code, a "spaces/..." name, or a https://meet.google.com/... url.
 */
function showSpaceConfig(codeOrName) {
  if (!codeOrName) throw new Error('Pass a meeting code, a spaces/... name, or a Meet url.');
  var id = String(codeOrName).trim();
  id = codeFromMeetUri_(id) || id;                       // url -> code
  var name = id.indexOf('spaces/') === 0 ? id : 'spaces/' + id;

  var space = meet_('GET', 'v2/' + name);
  console.log('space        : %s', space.name);
  console.log('meetingCode  : %s', space.meetingCode);
  console.log('meetingUri   : %s', space.meetingUri);
  console.log('config as stored by Google:\n%s', JSON.stringify(space.config || {}, null, 2));

  // Absent keys are the tell: Google omits defaults, so a config that never applied comes back {} or near-empty.
  var c = space.config || {};
  var mr = c.moderationRestrictions || {};
  var ac = c.artifactConfig || {};
  var lines = ['--- the ones we set ---'];
  [['accessType', c.accessType],
   ['moderation', c.moderation],
   ['presentRestriction', mr.presentRestriction],
   ['defaultJoinAsViewerType', mr.defaultJoinAsViewerType],
   ['attendanceReport', c.attendanceReportGenerationType],
   ['autoRecording', (ac.recordingConfig || {}).autoRecordingGeneration],
   ['autoTranscription', (ac.transcriptionConfig || {}).autoTranscriptionGeneration],
   ['autoSmartNotes', (ac.smartNotesConfig || {}).autoSmartNotesGeneration]
  ].forEach(function (row) {
    // console.log understands %s but not the %-26s width form, so pad here.
    lines.push((row[0] + '                          ').slice(0, 26) + ' ' +
      (row[1] === undefined ? 'NOT SET (Google returned no value)' : row[1]));
  });
  console.log(lines.join('\n'));

  try {
    // space.name, not the code we were given: the members endpoints require the canonical spaces/{id} and reject
    // the meetingCode alias with 403 "Permission denied on resource space" — which reads exactly like a
    // permissions problem and is not one. spaces.get accepts either, which is what makes the trap easy to fall in.
    var members = (meet_('GET', 'v2beta/' + space.name + '/members?pageSize=100').members) || [];
    console.log('--- members (%s) ---\n%s', members.length,
      members.map(function (m) { return (m.role || 'ROLE_UNSPECIFIED') + '  ' + (m.email || m.user || m.name); }).join('\n'));
  } catch (err) {
    console.error('members lookup failed: %s', String(err && err.message || err));
  }
  return space;
}

/**
 * Zero-argument wrapper for showSpaceConfig, because the editor's Run button cannot pass arguments.
 *
 * Inspects the INSPECT_MEETING script property if it is set (a meeting code, space name or Meet url), otherwise
 * the most recently created upcoming booking on CALENDAR_ID.
 */
function showLatestSpaceConfig() {
  var cfg = config_();
  var pinned = PropertiesService.getScriptProperties().getProperty('INSPECT_MEETING');
  if (pinned) {
    console.log('Using the INSPECT_MEETING script property: %s', pinned);
    return showSpaceConfig(pinned);
  }

  var found = Calendar.Events.list(cfg.calendarId, {
    timeMin: new Date(Date.now() - 24 * 3600 * 1000).toISOString(),
    singleEvents: true,
    maxResults: 100
  });
  var candidates = ((found && found.items) || []).filter(function (e) {
    return e.status !== 'cancelled' && (((e.conferenceData || {}).entryPoints || [])[0] || {}).uri;
  });
  if (!candidates.length) {
    throw new Error('No upcoming event with a Meet on calendar "' + cfg.calendarId + '". Set the INSPECT_MEETING ' +
      'script property to a meeting code instead.');
  }
  // Newest first by creation time — the booking you just made.
  candidates.sort(function (a, b) { return String(b.created || '').localeCompare(String(a.created || '')); });
  var event = candidates[0];
  var uri = event.conferenceData.entryPoints[0].uri;
  console.log('Latest booking: "%s" (%s), created %s\n%s\n',
    event.summary, event.id, event.created, uri);
  return showSpaceConfig(uri);
}

function showConfig() {
  var cfg = config_();
  console.log(JSON.stringify({
    calendarId: cfg.calendarId,
    calendarSummary: safeCalendarSummary_(cfg.calendarId),
    approverDomains: cfg.approverDomains,
    summaryPrefix: cfg.summaryPrefix,
    spaceConfig: cfg.spaceConfig,
    sharedSecretSet: !!cfg.sharedSecret,
    testCohostEmail: cfg.testCohostEmail,
    conferenceMode: cfg.conferenceMode,
    cohostMode: cfg.cohostMode,
    runningAs: Session.getEffectiveUser().getEmail()
  }, null, 2));
}

/** Lists the account's calendars so you can copy the "Community Meets" id into CALENDAR_ID. */
function listCalendars() {
  var items = Calendar.CalendarList.list().items || [];
  items.forEach(function (c) { console.log(c.summary + '  →  ' + c.id + (c.primary ? '  (primary)' : '')); });
}

// ---------------------------------------------------------------------------
// Internals
// ---------------------------------------------------------------------------

function meet_(method, path, body) {
  var res = UrlFetchApp.fetch(MEET_API + path, {
    method: method,
    contentType: 'application/json',
    muteHttpExceptions: true,
    headers: { Authorization: 'Bearer ' + ScriptApp.getOAuthToken() },
    payload: body ? JSON.stringify(body) : undefined
  });
  var code = res.getResponseCode();
  var text = res.getContentText();
  if (code >= 300) throw new Error(method + ' ' + path + ' → HTTP ' + code + ': ' + text);
  return text ? JSON.parse(text) : {};
}

/**
 * Per-meeting switches from the form layered over the default space config. Returns a copy.
 *
 * Both are opt-in and default OFF when the caller says nothing: recording or transcribing a community meetup that
 * nobody asked to have recorded is the worse failure. Transcription and Gemini notes are independent switches in
 * the API but one editorial decision here, so one flag drives both — setting only one would quietly produce notes
 * for a meeting that asked for neither.
 *
 * The notes/transcript LANGUAGE is not settable through the API. Meet uses the account's default Meeting Records
 * language, so set that once for the host account in Meet settings (English, for us).
 */
function applyPerMeetingOptions_(req, base) {
  var cfgCopy = JSON.parse(JSON.stringify(base));
  var record = flag_(req.recordMeeting);
  var transcribe = flag_(req.transcribeMeeting);
  cfgCopy.artifactConfig = cfgCopy.artifactConfig || {};
  cfgCopy.artifactConfig.recordingConfig = { autoRecordingGeneration: record ? 'ON' : 'OFF' };
  cfgCopy.artifactConfig.transcriptionConfig = { autoTranscriptionGeneration: transcribe ? 'ON' : 'OFF' };
  cfgCopy.artifactConfig.smartNotesConfig = { autoSmartNotesGeneration: transcribe ? 'ON' : 'OFF' };
  return cfgCopy;
}

/** Absent, null, "false" and false are all off. Anything else is on. */
function flag_(value) {
  if (value === undefined || value === null || value === '') return false;
  if (typeof value === 'string') return value.toLowerCase() !== 'false' && value !== '0';
  return !!value;
}

/** Builds "config.accessType,config.moderation,config.moderationRestrictions.presentRestriction,..." from the object. */
function fieldMask_(obj, prefix) {
  var paths = [];
  Object.keys(obj).forEach(function (k) {
    var v = obj[k];
    var path = prefix + '.' + k;
    if (v !== null && typeof v === 'object' && !Array.isArray(v)) paths = paths.concat(fieldMask_(v, path).split(','));
    else paths.push(path);
  });
  return paths.join(',');
}

function resolveTimes_(req) {
  var startLocal = normaliseLocal_(req.startLocal);
  var endLocal = req.endLocal ? normaliseLocal_(req.endLocal) : null;
  if (!endLocal) {
    // Wall-clock arithmetic: treat the local time as UTC, add minutes, format back. Only wrong for a
    // meeting that straddles a DST switch at 02:00–03:00 local, which we accept.
    var d = new Date(startLocal + 'Z');
    d.setUTCMinutes(d.getUTCMinutes() + Number(req.durationMinutes || 60));
    endLocal = d.toISOString().replace(/\.\d{3}Z$/, '');
  }
  return { startLocal: startLocal, endLocal: endLocal };
}

/**
 * Copies a Meet link out of an event's conferenceData into state. Returns true if it found one.
 *
 * Two shapes: a conference we attached has entryPoints and NO conferenceId; one Calendar minted via createRequest
 * has conferenceId (and entryPoints once it stops being 'pending'). Both have to work — bookings exist in both
 * shapes, and CONFERENCE_MODE can be switched at any time.
 */
function readConferenceInto_(event, state) {
  var conf = (event || {}).conferenceData || {};
  var uri = ((conf.entryPoints || [])[0] || {}).uri || null;
  if (uri) {
    state.meetUri = uri;
    state.meetingCode = codeFromMeetUri_(uri) || conf.conferenceId || state.meetingCode;
  } else if (conf.conferenceId) {
    state.meetingCode = conf.conferenceId;
    state.meetUri = 'https://meet.google.com/' + conf.conferenceId;
  }
  return !!state.meetUri;
}

/**
 * Canonical form of a record id, for use as the idempotency key.
 *
 * Extended-property matching is exact, so "8EB2F29D-3584-..." and "8eb2f29d35844f5a..." are different keys and a
 * second call for one record would look like a first and book a duplicate. The Umbraco workflow sends
 * Guid.ToString("N") — 32 lower hex digits, no hyphens — but the approver form takes recordId from a URL query
 * param, where a hyphenated or upper-case guid pasted out of the backoffice is entirely plausible. Normalising on
 * both the write and the lookup makes every spelling of an id the same key.
 *
 * Applied to non-guid ids too (the form's own "manual-<uuid>"); harmless, because both sides normalise.
 */
function recordKey_(recordId) {
  return String(recordId == null ? '' : recordId).trim().toLowerCase().replace(/-/g, '');
}

/** "https://meet.google.com/abc-defg-hij" -> "abc-defg-hij". Null when it isn't a Meet url. */
function codeFromMeetUri_(uri) {
  var m = /^https?:\/\/meet\.google\.com\/([^\/?#]+)/.exec(String(uri || ''));
  return m ? m[1] : null;
}

function normaliseLocal_(s) {
  // Accept "2026-09-17T19:00" or "2026-09-17T19:00:00"; reject offsets — timeZone carries the zone.
  var m = /^(\d{4}-\d{2}-\d{2})T(\d{2}):(\d{2})(?::(\d{2}))?$/.exec(String(s).trim());
  if (!m) throw new Error('startLocal/endLocal must be wall-clock "YYYY-MM-DDTHH:MM[:SS]" without an offset, got "' + s + '"');
  return m[1] + 'T' + m[2] + ':' + m[3] + ':' + (m[4] || '00');
}

function validateRequest_(req) {
  var problems = [];
  if (!req.recordId) problems.push('recordId missing');
  if (!req.title || String(req.title).trim().length < 3) problems.push('title missing');
  if (req.durationMinutes !== undefined && !(Number(req.durationMinutes) >= 5 && Number(req.durationMinutes) <= 480)) problems.push('durationMinutes must be 5–480');
  if (!req.cohostEmail || !/^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(req.cohostEmail)) problems.push('cohostEmail invalid');
  if (!req.timeZone) problems.push('timeZone missing');
  try { normaliseLocal_(req.startLocal); } catch (e) { problems.push(e.message); }
  if (req.endLocal) { try { normaliseLocal_(req.endLocal); } catch (e) { problems.push(e.message); } }
  else if (!(Number(req.durationMinutes) > 0)) problems.push('durationMinutes or endLocal required');
  return problems;
}

/**
 * The guest-facing event description. Every line about behaviour is derived from the config actually being applied
 * rather than hardcoded, because these lines have been wrong before: they claimed "anyone with the link can join
 * directly" under accessType TRUSTED, and "you are co-host" in a mode that adds no co-host.
 */
function buildDescription_(req, spaceCfg, cohostMode) {
  var cfg = spaceCfg || {};
  var restrictions = cfg.moderationRestrictions || {};
  var hostsOnlyPresenting = restrictions.presentRestriction === 'HOSTS_ONLY';
  // Only claim co-host when the script is actually going to add one. Saying it unconditionally was a real bug: the
  // description shipped "You are co-host" in a mode that added none.
  var cohostExpected = cohostMode === 'api' || cohostMode === 'auto';
  var lines = [
    req.description || '',
    '',
    'Requested by: ' + (req.requesterName || req.cohostEmail),
    '',
    '— How to run this Meet —',
    (cfg.accessType === 'OPEN'
      ? '• Anyone with the link joins directly.'
      : '• You join straight from this invite. Anyone else with the link has to ask, and a host admits them — ' +
        'simultaneous knocks collapse into one "Admit all".'),
    (cohostExpected
      ? '• You are a co-host, so you have the Host controls (shield icon, bottom right). If they are not there, ' +
        'reply to this invite and we will fix it before the meeting.'
      : null),
    (hostsOnlyPresenting
      ? '• Only hosts and co-hosts can share their screen. Promote anyone else who needs to present from the ' +
        'People panel.'
      : '• Anyone in the call can share their screen. A host can restrict that from the Host controls (shield icon, ' +
        'bottom right) if you would rather they could not.'),
    artifactLine_(req),
    '• A host can turn any of those on or off from inside the call.',
    '',
    'Booked via the Umbraco community site. Questions: community@umbraco.com'
  ];
  return lines.filter(function (line) { return line !== null; }).join('\n');
}

/**
 * One line describing what will be captured, from the request's own flags. Kept separate because getting this
 * wrong tells attendees a meeting is not being recorded when it is.
 */
function artifactLine_(req) {
  var record = flag_(req.recordMeeting);
  var transcribe = flag_(req.transcribeMeeting);
  if (record && transcribe) {
    return '• This meeting is recorded and transcribed, and Gemini takes notes. All of it starts automatically — ' +
      'say so at the start.';
  }
  if (record) {
    return '• A recording starts automatically — say so at the start. It is not transcribed and Gemini notes are off.';
  }
  if (transcribe) {
    return '• This meeting is transcribed and Gemini takes notes, starting automatically — say so at the start. ' +
      'It is not recorded.';
  }
  return '• This meeting is NOT recorded, NOT transcribed, and Gemini notes are off.';
}

/** True when the email's domain is one of APPROVER_DOMAIN. Empty email (anonymous deployment) is never an approver. */
function isApprover_(email, cfg) {
  var domain = String(email || '').toLowerCase().split('@')[1];
  return !!domain && cfg.approverDomains.indexOf(domain) !== -1;
}

function safeCalendarSummary_(id) {
  try { return Calendar.Calendars.get(id).summary; } catch (e) { return 'NOT FOUND: ' + e.message; }
}

function constantTimeEquals_(a, b) {
  if (a.length !== b.length) return false;
  var r = 0;
  for (var i = 0; i < a.length; i++) r |= a.charCodeAt(i) ^ b.charCodeAt(i);
  return r === 0;
}

function json_(obj) {
  return ContentService.createTextOutput(JSON.stringify(obj)).setMimeType(ContentService.MimeType.JSON);
}

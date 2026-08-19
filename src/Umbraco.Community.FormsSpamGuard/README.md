# Umbraco.Community.FormsSpamGuard

A multi-signal anti-spam **field type** for Umbraco Forms. Add one field to a form and it runs three independent
bot checks — none of which a visitor ever sees.

## Why

Umbraco Forms ships a honeypot, but it is trivially identifiable, so bots that scrape and parse the form walk
straight past it. The field it renders is:

```html
<div class="umbraco-forms-hidden" aria-hidden="true">
    <input type="text" name="{formIdWithoutDashes}" />
</div>
```

Five things give it away without a bot needing to understand the page: the name is **exactly 32 hex characters**
(no real form uses such a name, so one rule defeats it on every Umbraco site at once), the wrapper class is
literally `umbraco-forms-hidden`, it carries `aria-hidden`, it is the only input with no label/id/field wrapper,
and it is `display: none`. Worse, the name is **stable for the life of the form**, so a bot that scrapes once can
reuse the exclusion forever.

This package fixes each of those, and adds two signals a scraper cannot fake by reading markup.

## The three signals

Each is independently toggleable per form.

| Signal | Default | What it catches |
| --- | --- | --- |
| **Decoy field** | On | Anything filling inputs indiscriminately |
| **Timing check** | On | Instant posts, stale cached pages, tokens reused beyond the window |
| **JavaScript proof** | **Off** | Scrapers that fetch and parse without running scripts |

### The token that makes it work

Every render emits one hidden input holding a Data Protection-protected payload of
`{ renderedUtc, decoyFieldName, nonce }`.

That is the whole trick: because the decoy's **name travels back inside a signed payload**, it can be randomised
on every single render and the server still knows exactly which key to check. No derivation scheme, no
server-side state, and the timestamp becomes tamper-proof for free. A scraper cannot learn the decoy name once
and reuse it.

### Decoy field

Rendered with a plausible name, a real `<label>` and `id`, and hidden off-screen rather than with `display: none`.
The name is drawn at random per render from a pool of reference-style names (`enquiryReference`, `topicCode`,
`caseRef`, …) plus a random suffix.

**Every name and the default label are deliberately meaningless to browser autofill.** This is the one setting
that can cost you real submissions. Names like `emailConfirm`, `websiteUrl` or `faxNumber` look more plausible to
a bot, but they sit in the autofill vocabulary (`email`, `url`, `tel`) — and `autocomplete="off"` will not save
you, because Chrome ignores it for those heuristics and password managers ignore it outright. Autofill reads
nearby **label text** too, so the label matters as much as the name. If you override either, avoid anything
containing email, name, phone/tel, address, city, zip/postal, country, company/organization, url/website/homepage,
card, username or password.

The asymmetry is what decides it: a missed bot costs one spam message, a false positive costs a real person's
enquiry with no explanation.

The field also sets `SupportsMandatory = false`, so the Mandatory checkbox never appears. It posts no value under
its own key, so Forms' built-in required check could never pass for it.

It deliberately **keeps `aria-hidden="true"` and `tabindex="-1"`**. That is one tell a sophisticated bot can
still read, and removing it would genuinely help — but it would also mean screen readers announcing a fake
field to real people. That trade is not worth making.

### Timing check

Rejects submissions arriving sooner than `Minimum fill time` (default 3s) or later than `Maximum form age`
(default 2h) after the form was rendered. On multi-page forms each step issues a fresh token, so the minimum
applies per step.

**Do not mistake the upper bound for replay protection.** Tokens are not single-use and nothing invalidates one
after a successful submission, so a bot that fetches the page once can reuse the same token — decoy blank, a
polite pause — for the whole window and pass every signal. Raising `Maximum form age` does not weaken anything
that was protecting you, and lowering it does not buy replay protection; it only bounds how long one harvested
token stays usable, and rejects genuine visitors who left the tab open. In practice a fresh token costs a bot a
single GET, so the minimum fill time is the setting doing the real work.

### JavaScript proof of presence

**This proves that a JavaScript engine ran on the page, and nothing more.** The algorithm ships in this package,
so anyone can replicate it — it is obfuscation, not cryptography. Its value is excluding the large population of
scrapers that never execute scripts.

It is **off by default** because switching it on means a visitor with JavaScript disabled cannot submit the form
at all. Turn it on knowingly.

## Installing

Reference the project (or package) — the composer self-registers. Nothing else is required; the field does
nothing until an editor places it on a form.

## Settings

| Setting | Default | Notes |
| --- | --- | --- |
| Decoy field | On | |
| Timing check | On | |
| Require JavaScript | Off | Blocks no-JS visitors when on |
| Minimum fill time (seconds) | 3 | |
| Maximum form age (hours) | 2 | |
| Decoy field label | `Enquiry reference` | Must stay outside autofill vocabulary — see below |
| Error message | generic fallback | Keep it generic — see below |
| Log submitted values on rejection | Off | Writes the visitor's other field values into the **rejection** log, so a false positive can be recovered by hand — see below |
| Save fill duration | Off | Stores fill time (e.g. `71.8s`) on **successful** submissions, for tuning the minimum |

Site-wide options bind from `UmbracoCommunity:FormsSpamGuard`:

```json
{
  "UmbracoCommunity": {
    "FormsSpamGuard": {
      "DataProtectionPurpose": "Umbraco.Community.FormsSpamGuard.v1",
      "RejectionLogLevel": "Warning",
      "AcceptanceLogLevel": "Information"
    }
  }
}
```

## Three things that will silently stop it working

1. **Place the field on the form's last page.** Umbraco Forms only validates fieldsets belonging to the step being
   submitted, so a field on an earlier page never runs.
2. **Do not put it in a conditionally hidden fieldset.** Hidden fieldsets are skipped entirely during validation.
3. **Do not output-cache a page containing the field.** Every visitor would receive the same render timestamp, and
   the maximum-age check would start rejecting genuine submissions once the cached entry aged past the limit.

## Prerequisite: shared Data Protection keys

The token is encrypted with ASP.NET Core Data Protection, so **every instance that serves this site must share a
Data Protection key ring.** Umbraco Cloud configures one; a hand-rolled load-balanced setup may not.

This is not a nice-to-have. If instance A renders the form and the visitor's POST lands on instance B, B cannot
decrypt the token and the submission is rejected. With two instances that is roughly half of all genuine
submissions failing, intermittently, looking random.

The failure is deliberately fail-closed: if the token cannot be read, the submission is rejected. That is right
for security, but it means a broken key ring rejects **every** submission rather than letting spam through. The
log distinguishes the two causes so this is diagnosable — see [Failure behaviour](#failure-behaviour).

Nothing can detect this automatically, so it is on you to confirm it.

## Failure behaviour

A rejected submission gets a **generic** error message; the **log** names the signal that actually tripped.

This differs from Forms' built-in honeypot, which fakes success and silently discards the submission. Silent
discard is the worse failure: a false positive — autofill, an unusually quick genuine visitor, a stale page —
costs a real enquiry with no visible trace. A visible generic error lets a real person retry while telling a bot
nothing about which of the three checks caught it.

### Recovering a false positive

A rejected submission is **never stored**, so a real enquiry caught by a false positive has nowhere to be
recovered from by default — the visitor sees a generic error and, if they don't retry, it's simply gone.

Turn on **Log submitted values on rejection** to change that: every rejection then also logs the visitor's other
field values (`Caption: 'value'` pairs) so you can read them back out of the log and follow up by hand. Any field
the editor marked **Contains sensitive data** in Forms is always left out, regardless of this setting.

This is off by default because it writes personal data into your application logs, which typically have
different retention and access controls than stored form submissions — decide deliberately, per form, whether
that trade-off is acceptable, rather than assuming your log retention already matches your data retention policy.

### Reading the logs

A rejected submission is **never stored** — there is no record, no entry in the list, and workflows never run.
The log line is the only record that it happened, so this is where you look.

**Every outcome is logged, not just rejections.** An earlier version only logged when a submission was rejected,
which made "nothing in the log" ambiguous between "this passed" and "the field never ran at all" — misplaced on
a non-final page, sitting in a hidden fieldset, or every signal switched off. Both outcomes now log, at different
levels so they can be filtered independently: rejections at `Warning` (configurable via `RejectionLogLevel`),
acceptances at `Information` (configurable via `AcceptanceLogLevel`). Neither log line reveals anything to the
visitor — this is server-side only, same as the rejection reason always was.

Every rejection logs with a `Signal` property naming exactly what caught it:

| `Signal` | Means | What to do |
| --- | --- | --- |
| `TokenAbsent` | No token posted; never came from a rendered form | Nothing — ordinary bot traffic |
| `TokenUnreadable` | A token was posted but could not be decrypted | **Investigate.** Almost always a Data Protection key ring problem, and genuine submissions are being rejected for as long as it lasts |
| `DecoyFieldSignal` | The hidden decoy was filled in | Nothing, unless it correlates with real complaints — then suspect autofill |
| `SubmissionTimingSignal` | Too fast, or older than the maximum | Check the elapsed time in the message before tightening the minimum |
| `JavaScriptTokenSignal` | Scripts did not run, or the answer was wrong | If this spikes after a deploy, suspect drift between `spam-guard.js` and the C# side |

Every acceptance logs with the same `Signal` property, naming why it passed rather than what caught it:

| `Signal` | Means |
| --- | --- |
| `Passed` | Every enabled signal ran and none of them objected |
| `AllSignalsDisabled` | Every signal is switched off on this field, so it let the submission through without evaluating anything |

If you expect a form to be checked and see neither a rejection nor a `Passed`/`AllSignalsDisabled` acceptance for
a submission you know happened, the field itself never ran — go back to the three things in
[the section above](#three-things-that-will-silently-stop-it-working).

In **Settings → Log Viewer**, these all work as queries:

```
SourceContext = 'Umbraco.Community.FormsSpamGuard.FieldTypes.SpamGuardField'
Signal = 'TokenUnreadable'
Signal = 'DecoyFieldSignal'
Signal = 'AllSignalsDisabled'
@Message like '%Spam guard rejected%'
@Message like '%Spam guard accepted%'
```

The first is the one to save — it shows every outcome from the field and nothing else. `Signal = 'TokenUnreadable'`
is the one worth alerting on: at anything above a trickle it is an outage, not a spam report. `Signal =
'AllSignalsDisabled'` is worth a one-off check the first time you see it — it usually means a form was set up
with every toggle turned off during testing and never turned back on.

## Relationship to the built-in honeypot

**There is no supported way to disable Forms' built-in honeypot.** `UmbracoFormsController` calls its check
unconditionally, and the method is `private`, so it cannot be overridden. No configuration setting or
`FormViewModel` flag exists.

This field is fully independent of it and the two coexist without interference — the built-in one is simply a
free extra decoy. The only way to remove it is to override the theme's `Form.cshtml` and delete the markup, which
is a decision for the site, not something this package will do to your theme.

## Development

```bash
# Server
dotnet build

# Tests
dotnet test tests/Umbraco.Community.FormsSpamGuard.Tests

# Backoffice preview bundle and static assets
cd src/Umbraco.Community.FormsSpamGuard/Client
npm ci && npm run build

# Frontend tests (pins spam-guard.js against the C# implementation)
npm test
```

`Client/public/spam-guard.js` and `spam-guard.css` are **source files**, copied into `wwwroot/App_Plugins/` by
the Vite build along with the bundle. The `wwwroot/App_Plugins/` folder is generated and gitignored.

`spam-guard.js` must stay in step with `SpamGuardTokenService.ComputeJavaScriptAnswer`. Both sides are pinned to
the same fixture (`"abc123"` -> `"MzIxY2Jh"`): `SpamGuardTokenServiceTests` covers the C# side, and the Vitest
suite in `Client/` runs the **real shipped file** against jsdom. Edit one without the other and one of the two
fails.

Note that this repo's CI builds but does not run tests, so that guard only fires for whoever runs them locally.
Run both before touching either side.

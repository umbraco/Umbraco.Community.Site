# UmbracoCommunity.ActiveCampaign

An Umbraco Forms **workflow type**, *Subscribe to ActiveCampaign*, that adds a form's submitter to an ActiveCampaign
newsletter list through the ActiveCampaign v3 REST API, and optionally tags the contact. No Zapier; Forms' built-in *Send form to URL* can't do it,
because ActiveCampaign wants an `Api-Token` header, a JSON body, and a second call that uses the contact id from the
first.

> [!WARNING]
> **This subscribes people immediately and bypasses ActiveCampaign's double opt-in** (it adds the contact to the list
> with `status: 1`, "active"), so there's no confirmation step: the form's consent checkbox is the only record of consent.
> Every form this workflow is attached to **must** have an explicit consent checkbox that is **unticked by default**,
> and that checkbox's alias **must** be set in the workflow's *Consent field alias* setting. Without it, everyone who
> submits the form is subscribed.

## What it does

On each run (typically *On Submit*):

1. If *Consent field alias* is set and the box isn't ticked → logs at Information, returns **Completed**, calls nothing.
2. Reads the email (and optional first/last name) from the entry. Missing or invalid email → logs a warning, **Failed**.
3. `POST {BaseUrl}contact/sync` with `{ "contact": { "email", "firstName", "lastName" } }` — an upsert by email, so
   repeat submissions are safe. Empty names are left out rather than sent blank, so they don't wipe a stored name.
4. Reads `contact.id` from the response.
5. `POST {BaseUrl}contactLists` with `{ "contactList": { "list": <List ID>, "contact": <id>, "status": 1 } }`.
   Any non-2xx from steps 3–5 is logged with its status code and body (the API key is never logged, and is scrubbed
   from bodies too), as are timeouts (10 s), network errors and unparseable responses → **Failed**, which shows on the
   entry's workflow audit and can be re-run from the backoffice. No tags are added.
6. If *Tag IDs* is set: for each tag, in order, `POST {BaseUrl}contactTags` with
   `{ "contactTag": { "contact": <id>, "tag": <Tag ID> } }`
   ([API reference](https://developers.activecampaign.com/reference/create-contact-tag)). A tag that fails (non-2xx,
   timeout, network error) is logged as a **warning** with the record id, tag id, status code and scrubbed body; the
   remaining tags are still tried, and the entry is **not** failed, because the contact is already subscribed.
7. **Completed**.

The ids (`list`, `contact`, `tag`) are sent as JSON numbers.

Email addresses aren't written to the log — log lines identify the entry by its record id.

## Configuration

`appsettings.json` ships the section with the account's API URL and an empty key:

```jsonc
"ActiveCampaign": {
  "BaseUrl": "https://umbraco.api-us1.com/api/3/",   // not a secret; committed
  "ApiKey": ""                                       // ActiveCampaign → Settings → Developer → API Access. Secret.
}
```

`BaseUrl` is the account URL from ActiveCampaign → *Settings* → *Developer* plus `/api/3/`. A missing trailing slash is
fine, but it has to include `/api/3`. Only `ApiKey` needs setting per environment. If either value is missing you get a warning at startup, the workflow type reports itself as not configured
to the backoffice (which Forms documents as showing it disabled in the workflow picker), and any workflow already attached to a form fails cleanly.

### Locally: `appsettings.Local.json`

Put the real key in the gitignored `src/UmbracoCommunity.Web.UI/appsettings.Local.json`, same as the other
integrations' secrets:

```json
{
  "ActiveCampaign": {
    "ApiKey": "<your key>"
  }
}
```

That file is only loaded when `ASPNETCORE_ENVIRONMENT=Local`, i.e. the **`Kestrel [ENV: Local]`** launch profile
(`dotnet run --launch-profile "Kestrel [ENV: Local]"` from `src/UmbracoCommunity.Web.UI`). The default profile runs as `Development`, which doesn't
read it.

Alternative for the `Development` profile: `dotnet user-secrets set "ActiveCampaign:ApiKey" "<your key>" --project src/UmbracoCommunity.Web.UI`. Web.UI has a `UserSecretsId`, but ASP.NET Core only loads user secrets in
`Development`, so they're **ignored under the Local profile**.

### Deployed (Umbraco Cloud)

Set `ActiveCampaign__ApiKey` as an environment variable / secret in the Cloud portal for each environment. Never
commit it. (`ActiveCampaign__BaseUrl` can override the committed URL if an environment ever needs a different account.)
Both are configuration rather than workflow settings deliberately: workflow settings travel with the form (Deploy transfers, the backoffice API).

## Adding it to a form

1. Build the form with at least an email field, plus a consent checkbox that is **unticked by default** (a *Data
   consent* or plain *Checkbox* field; wording that says plainly that they'll receive the newsletter). Note the aliases
   (the field's padlocked alias input, next to *Name*).
2. Form → *Workflows* → *On Submit* → *Add workflow* → **Subscribe to ActiveCampaign** (under *Services*).
3. Settings:

   | Setting | |
   |---|---|
   | List ID | **Required.** The numeric id of the list — open it in ActiveCampaign, it's the number in the URL. |
   | Email field alias | Defaults to `email`. |
   | First name / Last name field alias | Optional. |
   | Consent field alias | The consent checkbox's alias. **Set this** — see the warning at the top. |
   | Tag IDs | Optional. Comma-separated numeric tag ids to add to the contact once it's subscribed, e.g. `12, 34`. Spaces are fine and duplicates are ignored; anything that isn't a positive whole number is rejected when you save. Leave blank for no tags. |

4. Leave *Exclude sensitive data* unticked if the email field is marked sensitive, or the workflow won't see it.

### Finding a tag's ID

Tags are referenced by numeric id, not by name. One way to look an id up is the API's
[list tags](https://developers.activecampaign.com/reference/retrieve-all-tags) endpoint, which takes a `search` filter
on the tag name (a "contains" match, so check the `tag` field of each result):

```bash
curl -H "Api-Token: <your key>" "https://umbraco.api-us1.com/api/3/tags?search=<tag name>"
```

Each entry in the response's `tags` array has an `id`; that's the value for *Tag IDs*. The tag has to exist already —
this workflow doesn't create tags.

A consent alias that doesn't match any field on the form fails every entry (rather than quietly subscribing no one), so
a typo shows up straight away in the workflow audit. You can also put a Forms workflow *condition* on the consent field
as well; then Forms itself records the run as skipped.

## Why "consent not given" returns Completed

Forms records each workflow's result on the entry's audit trail and raises `WorkflowExecutionFailedNotification` for
**Failed**. Someone declining the newsletter hasn't done anything wrong, so reporting it as a failure would bury real
failures and alert anyone watching for them. Forms' own *SkippedDueToConditions* status can't be used here: it's only
set by Forms when a workflow condition excludes the run. A workflow type that returns it trips an
`ArgumentOutOfRangeException` in Forms 18.1.1's completed-notification step, which Forms logs and records as an extra
Failed audit entry.

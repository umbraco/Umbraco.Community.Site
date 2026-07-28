---
tags: [umbraco-cloud, deployment, configuration, version-control, ef-core]
---

# Syncing custom backoffice configuration across environments

> **Builds on:** the Block Restrictions package, whose rules live in a custom database table (see [Configuration that inherits down the content tree](../foundations/content-tree-inherited-config.md) for how those rules resolve). This refinement is about a problem that hits *any* package storing its own config: how does config set in one environment reach the others, and stay in version control?

You've built a feature whose configuration lives in your own table — for us, block-restriction rules keyed by document type. It works beautifully on your machine. Then someone asks the obvious question: a rule you set up locally needs to be on staging and production too, and ideally it should be reviewable in a pull request like the rest of the codebase. Suddenly "it's in the database" is a problem, not a feature. This tutorial is how Block Restrictions makes its configuration travel between environments and live in git.

## The problem

Umbraco already moves things between environments. Content and schema (document types, data types) ride the deploy pipeline — the reconcile that runs on each deploy — as uSync or Umbraco Deploy `.uda` files (per-entity artefacts committed to the repo and imported into each environment). That's how a new document type you create locally shows up on production.

Block-restriction rules don't get any of that. They live in a custom EF Core table (`BlockRestrictionStore`), keyed by document-type GUID. Two consequences:

- **They're stranded.** A rule configured in the backoffice on one environment stays on that environment. There's no mechanism carrying it to the next.
- **They're invisible to git.** A teammate can't review "the Blog Post block menu changed" in a PR, and you can't roll a rule change back alongside the code that depended on it.

"Just configure it again in each environment" is the non-answer: it doesn't scale past a couple of rules, and the environments drift the moment someone forgets.

## Why the obvious fix doesn't work

- **Piggyback on the deploy pipeline.** Emit a `.uda` file like Umbraco's own entities. You don't own that format, and it's tied to Umbraco's deploy internals — a custom table has no business pretending to be a schema entity. Coupling to it would be fragile and is exactly the kind of host-internals dependency a self-contained package should avoid.
- **Auto-import the files into the database on boot.** Tempting — drop JSON files in the repo, import them on startup, done. But importing is **destructive**: a clean sync has to delete DB rules that no longer have a matching file (orphans — a database rule with no file behind it). Run that silently on every boot and you'll eventually wipe a rule someone added in the backoffice and hadn't exported yet. Import needs to be a deliberate, *reviewed* action, not a startup surprise.
- **Watch the files and sync both ways.** Bidirectional auto-sync invites a feedback loop — a save writes a file, a watcher re-imports it, which triggers a save — and re-runs that destructive delete on every file change. Too clever, too dangerous.

The shape that falls out: write to disk automatically (safe, one-directional), but pull *from* disk only on an explicit, previewed command.

## Our approach

The database stays the runtime source of truth. On top of it:

1. **DB → disk, automatically and one-way.** Every save or delete in the backoffice also writes (or removes) a JSON file at `umbraco/BlockRestrictions/{alias}.json`. Those files are the version-controlled artefact — commit them, and they travel with the repo to every environment.
2. **Disk → DB, manually and reviewed.** A dashboard shows a categorised diff (add / update / unchanged / delete / unknown-alias) and you click Apply. The destructive deletes are shown *before* they happen, and there's no watcher to loop on.
3. **A zip escape hatch for locked-down environments.** Where you can't put a file on the server's disk yourself (Umbraco Cloud — no FTP/SSH), the same rules move as a zip through the authenticated dashboard.

The logic lives in [`BlockRestrictionService`](../../../src/UmbracoCommunity.BlockRestrictions/BlockRestrictionService.cs) (sync + import/export) and [`BlockRestrictionFileService`](../../../src/UmbracoCommunity.BlockRestrictions/Infrastructure/BlockRestrictionFileService.cs) (the file I/O).

## Walkthrough

### Step 1 — Write to disk on every change

Saving a rule upserts the database row, then mirrors it to a file. Deleting does the reverse. That's the *only* automatic direction:

```csharp
public async Task SaveConfigAsync(Guid documentTypeKey, List<string> allowedAliases)
{
    await _store.UpsertAsync(documentTypeKey, allowedAliases);   // DB is the runtime source

    var contentType = _contentTypeService.Get(documentTypeKey);
    if (contentType != null)
        _fileService.SaveRuleFile(contentType.Alias, allowedAliases);   // mirror to disk
}
```

The file write is best-effort — wrapped in a try/catch that logs rather than throws, because the DB write is what matters at runtime and a disk hiccup shouldn't fail the editor's save. Note the file is keyed by **alias**, not the GUID the DB uses:

```json
{ "DocumentTypeAlias": "blogArticle", "AllowedBlocks": ["calloutBlock", "imageBlock", "richTextBlock"] }
```

### Step 2 — The files are the thing you version

`BlockRestrictionFileService` writes to `{ContentRootPath}/umbraco/BlockRestrictions/`, one file per document type, **aliases sorted alphabetically** so a rule change is a clean one-line git diff rather than a reordering noise-storm:

```csharp
AllowedBlocks = allowedAliases.OrderBy(a => a, StringComparer.OrdinalIgnoreCase).ToList()
```

Commit those files. Now the configuration is reviewable in a PR, rolls back with the code, and — because it's in the repo — lands on every environment the repo deploys to. Aliases rather than GUIDs is the deliberate choice that makes this readable and portable; a GUID-keyed file would diff like noise and wouldn't survive moving between environments where keys could differ.

### Step 3 — Import is a reviewed diff, never automatic

Getting the files *into* a target environment's database is an explicit action with a preview in front of it. `PreviewFileImportAsync` (on `BlockRestrictionService`) compares every file against the current DB rules and buckets the result:

```csharp
// toAdd        — file exists, no DB rule
// toUpdate     — file and DB rule differ
// unchanged    — they match
// toDelete     — DB rule with no file (orphan — will be removed on apply)
// unknownAlias — file names a document type this environment doesn't have
```

`ApplyFileImportAsync` then upserts from the files and deletes the orphans — once, busting the cache at the end. Because the `toDelete` bucket is shown in the preview, nobody is surprised by a removal. This is the single design decision that the "auto-import on boot" alternative gets wrong: the deletes are real, so a human sees them first.

### Step 4 — The Cloud path: move rules as a zip

On a platform where you can't drop a file on the server's disk (Umbraco Cloud gives you no FTP/SSH), the dashboard offers the same sync as zip transfer over the authenticated API:

- **Export from Database** / **Export from Disk** — download the current rules (or the current files) as a zip, to inspect or carry elsewhere.
- **Upload ZIP** — `ImportZipToFiles` validates each `.json` entry and writes it into `umbraco/BlockRestrictions/` on the server.

Uploading only writes the *files* — you then run the same Preview → Apply from Step 3 to reach the database. It's the same two-step, reviewed flow; the zip is just how the files get onto a disk you can't otherwise touch.

## Alternatives we considered

- **Auto-apply the committed files on every boot.** The convenient version of Step 3. Rejected because import deletes orphaned rules, and silently doing that on startup would eventually wipe a backoffice-made rule someone hadn't committed. A reviewed, manual apply trades a little ceremony for not destroying data by surprise.
- **A two-way file watcher.** Keep DB and disk in lockstep automatically. Rejected for the feedback-loop risk (save → file → re-import → save) and because every file touch would re-run the destructive delete. One-way-auto-out, manual-in sidesteps both.
- **Emit `.uda` / ride Umbraco Deploy.** Make the rules look like first-class Umbraco entities. Rejected — we don't own that format, and binding a custom table to the host's deploy internals is the coupling a self-contained package should avoid.
- **DB only, re-enter per environment.** No files at all. Rejected: no version control, no review, guaranteed drift. This is the problem, not a solution.

## Trade-offs and known limits

- **Disk and DB can drift.** The DB→file write is best-effort (logged, not fatal), and nothing stops someone editing a file by hand or the database directly. The two are reconciled by the preview diff, not by a live guarantee — the diff *is* the reconciliation tool, and you're expected to run it.
- **A fresh environment doesn't apply itself.** Because import is deliberate, spinning up a new environment leaves the committed files sitting there until someone opens the dashboard and runs Preview → Apply once. Put that step in your deploy runbook; it's the easiest thing to forget.
- **Import needs the document types to exist first.** Files are keyed by alias, so a file naming a document type the target environment doesn't have yet lands in the `unknownAlias` bucket and is skipped. Deploy the schema (the document type) before importing rules that reference it.
- **Zip upload is still two steps.** Uploading writes files, not database rows — you always follow it with Preview → Apply. That's intentional (the review stays in the loop) but it's a step people miss.
- **It's more machinery than small, static config needs.** If your configuration is set once and never touched, or it's small enough to re-enter by hand without drifting, the DB-only approach this tutorial rejected may be perfectly fine. The file mirror and reviewed import earn their keep when rules change often enough that re-entering them is a chore and changes are worth reviewing.

## Where to go next

- **[Configuration that inherits down the content tree](../foundations/content-tree-inherited-config.md)** — how a rule, once in the database, resolves for any content node.
- **[The `UmbracoCommunity.BlockRestrictions` README](../../../src/UmbracoCommunity.BlockRestrictions/README.md)** — the dashboard walkthrough for the import/export and zip flows described here, click by click.
- **[`LESSONS_LEARNED.md`](../../LESSONS_LEARNED.md)** — the broader Umbraco Cloud deploy notes this sits alongside, plus how the package runs its own EF Core migrations on startup without racing the installer.

The general lesson outlives block restrictions: if your package owns configuration data, give it a version-controllable file representation and a *reviewed* path back into the database — automatic out, deliberate in. Do that, and the deploy that arrives with none of your rules stops being a story you have to tell.
</content>

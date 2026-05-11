# Umbraco Community Website

The source of the new Umbraco community site that is currently being built to replace [community.umbraco.com](https://community.umbraco.com).

## Local Development

Clone the repository locally.

### Quick start (recommended for new contributors)

The fastest way to get a working local environment is to seed it from the live community site's snapshot, then start the dev servers:

```bash
node build.mjs reset       # back up any existing local DB and pull the latest snapshot
node build.mjs dev:dotnet  # build backoffice + start Vite dev server + dotnet run
```

`reset` downloads a recent export (schema + content + media) from the live community site, drops it at `umbraco/Deploy/import-on-startup.zip`, and renames any existing `Umbraco.sqlite.db` (and its `-shm`/`-wal` siblings) with a timestamp so the next boot installs fresh. The first `dev:dotnet` run after a seed takes a few minutes while Deploy imports the snapshot.

If you've already got a local database and just want to refresh content without nuking your DB, use `node build.mjs seed` instead of `reset`.

For day-to-day work, just run `node build.mjs` with no arguments and pick from the interactive menu — that's the easiest way to start each session.

### Other ways to run

If you'd rather skip the seed step or run things by hand, the dev script supports several modes:

```bash
node build.mjs dev:dotnet   # what Quick start uses
node build.mjs dev          # Vite dev server only (you run dotnet yourself)
```

If you prefer to run things separately (or run without the build script), you need two processes:

- cd to `src/UmbracoCommunity.Web.UI` and run `dotnet run`
- In a separate terminal, cd to `src/UmbracoCommunity.StaticAssets` and run `npm run dev`
  - Run `npm ci` first if you get errors about missing packages

See [BUILD.md](BUILD.md) for all build script options and launch profiles.

### Upgrading Packages

A CLI tool is provided to update all NuGet packages and regenerate Umbraco Deploy schema files.

```bash
# Build the tool
dotnet build tools/upgrade-umbraco

# Preview what would be updated (recommended first step)
tools\upgrade-umbraco\bin\Debug\net10.0\upgrade-umbraco.exe update-packages --dry-run

# Update packages and regenerate Deploy schema in one step
tools\upgrade-umbraco\bin\Debug\net10.0\upgrade-umbraco.exe all
```

See [tools/upgrade-umbraco/README.md](tools/upgrade-umbraco/README.md) for full documentation.

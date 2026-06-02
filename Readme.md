# Umbraco Community Website

The source of the Umbraco community site - [community.umbraco.com](https://community.umbraco.com).

## Local Development

Clone the repository locally.

### Quick start (recommended for new contributors)

The easiest way to get a working local environment is to run the build script and follow instructions:

```bash
node build.mjs
```

On a fresh clone, the script identifies there is no database so offers to download the latest community snapshot and run first-time setup for you. Say yes, then you will have a fully working website!

The whole thing takes a few minutes on first run. Subsequent runs reuse the local DB and skip straight to the dev servers.

#### Refreshing content later

Once you have a working database, the interactive menu also offers:

- **`seed`** — pull the latest snapshot and queue it for the next boot, keeping your existing DB schema.
- **`reset`** — rename `Umbraco.sqlite.db` (and its `-shm`/`-wal` siblings) aside with a timestamp, then re-run the same first-time setup as a clean clone.

Both are also available directly as `node build.mjs seed` / `node build.mjs reset`.

### Other ways to run

```bash
node build.mjs dev:dotnet   # what Quick start uses
node build.mjs dev          # Vite dev server only (you start dotnet yourself, e.g. in your IDE)
```

If you'd rather run things separately (or skip the build script entirely), you need two processes:

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

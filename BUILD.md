# Build Script

A unified build script that handles all frontend projects and optionally starts the .NET backend. Run it from the repository root:

```bash
node build.mjs [mode]
```

If no mode is provided, an interactive prompt lets you pick one.

## Modes

| Mode | What it does |
|---|---|
| `dev` | Builds backoffice extensions, then starts the Vite dev server with HMR |
| `dev:dotnet` | Same as `dev`, plus starts `dotnet run` with the Development launch profile |
| `local` | Builds all frontend projects for cloud deployment |
| `local:dotnet` | Same as `local`, plus starts `dotnet run` with the Local launch profile |

### dev / dev:dotnet (default)

The typical development workflow. Builds the two backoffice extension projects first (BlockRestrictions and Extensions), then starts the Vite dev server for StaticAssets. With `:dotnet`, the .NET backend starts alongside the Vite server.

```bash
# Just the frontend
node build.mjs dev

# Frontend + backend
node build.mjs dev:dotnet
```

### local / local:dotnet

Performs a full production-style build of all three frontend projects (BlockRestrictions, Extensions, and StaticAssets with `build:for:cloud`). Use this to test the cloud deployment build locally.

```bash
# Build only
node build.mjs local

# Build and run
node build.mjs local:dotnet
```

## Launch Profiles

The `:dotnet` variants use specific launch profiles from `src/UmbracoCommunity.Web.UI/Properties/launchSettings.json`:

| Mode | Launch profile | Environment |
|---|---|---|
| `dev:dotnet` | `Kestrel [ENV: Development - default]` | Development |
| `local:dotnet` | `Kestrel [ENV: Local]` | Local |

The Local environment reads from `appsettings.Local.json` (gitignored), which is where you store API keys and connection strings for local testing.

## Projects

The script manages three frontend projects:

| Project | Directory | Build command |
|---|---|---|
| BlockRestrictions | `src/UmbracoCommunity.BlockRestrictions/Client` | `npm run build` |
| Extensions | `src/UmbracoCommunity.Extensions/Client` | `npm run build` |
| StaticAssets | `src/UmbracoCommunity.StaticAssets` | `npm run build:for:cloud` (local) / `npm run dev` (dev) |

Missing `node_modules` are detected automatically and `npm ci` runs before the build.

## Output

All process output is color-coded by project name so you can tell them apart when running concurrently. Ctrl+C cleanly shuts down all running processes.

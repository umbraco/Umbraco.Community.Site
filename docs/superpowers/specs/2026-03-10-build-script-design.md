# Build Script Design

## Overview

A cross-platform Node.js build script (`build.mjs`) in the repo root that orchestrates npm builds for all three frontend projects and optionally runs the dotnet backend. Supports four modes via CLI argument or interactive prompt.

## Projects

| Project | Path | Dev command | Local command |
|---|---|---|---|
| BlockRestrictions | `src/UmbracoCommunity.BlockRestrictions/Client` | `npm run build` | `npm run build` |
| Extensions | `src/UmbracoCommunity.Extensions/Client` | `npm run build` | `npm run build` |
| StaticAssets | `src/UmbracoCommunity.StaticAssets` | `npm run dev` (long-running) | `npm run build:for:cloud` |
| Web.UI | `src/UmbracoCommunity.Web.UI` | `dotnet run` (long-running) | `dotnet run` (long-running) |

## Modes

### `dev`

1. Builds BlockRestrictions and Extensions concurrently.
2. Starts StaticAssets Vite dev server with prefixed output.

### `dev:dotnet`

1. Builds BlockRestrictions and Extensions concurrently.
2. Starts StaticAssets Vite dev server and `dotnet run` (Web.UI) concurrently, both with prefixed output.

### `local`

Builds all 3 frontend projects concurrently for cloud deployment. Uses `npm run build:for:cloud` for StaticAssets, `npm run build` for the other two.

### `local:dotnet`

Same as `local`, then starts `dotnet run` (Web.UI) with prefixed output.

### No argument

Prompts the user to pick a mode using Node's built-in `readline` (no dependencies).

## Behaviour

- **Auto `npm ci`**: Before building each project, checks if `node_modules/` exists. If missing, runs `npm ci` first.
- **Output**: Each concurrent process gets a colored label prefix (e.g. `[BlockRestrictions]`, `[Extensions]`, `[StaticAssets]`, `[Web.UI]`) so output is distinguishable. Child process colors are preserved via `FORCE_COLOR` and `DOTNET_SYSTEM_CONSOLE_ALLOW_ANSI_COLOR_REDIRECTION` environment variables.
- **Exit codes**: If any build fails, the script exits with a non-zero code. In dev modes, if a backoffice build fails, the long-running servers do not start.
- **Signal handling**: Ctrl+C cleanly kills all running child processes.
- **No dependencies**: Uses only Node.js built-ins (`child_process`, `fs`, `path`, `readline`).

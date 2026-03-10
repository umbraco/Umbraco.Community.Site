# Build Script Design

## Overview

A cross-platform Node.js build script (`build.mjs`) in the repo root that orchestrates npm builds for all three frontend projects. Supports two modes: `local` (production builds) and `dev` (backoffice builds + Vite dev server).

## Projects

| Project | Path | Dev command | Local command |
|---|---|---|---|
| BlockRestrictions | `src/UmbracoCommunity.BlockRestrictions/Client` | `npm run build` | `npm run build` |
| Extensions | `src/UmbracoCommunity.Extensions/Client` | `npm run build` | `npm run build` |
| StaticAssets | `src/UmbracoCommunity.StaticAssets` | `npm run dev` (foreground) | `npm run build:for:cloud` |

## Modes

### `local`

Builds all 3 projects for cloud deployment. All three can run concurrently or sequentially (order doesn't matter). Uses `npm run build:for:cloud` for StaticAssets, `npm run build` for the other two.

### `dev`

1. Builds BlockRestrictions and Extensions concurrently (both `npm run build`).
2. Once both complete, starts StaticAssets Vite dev server (`npm run dev`) in the foreground with inherited stdio, so the user sees HMR output and can Ctrl+C to stop.

### No argument

Prompts the user to pick a mode using Node's built-in `readline` (no dependencies).

## Behaviour

- **Auto `npm ci`**: Before building each project, checks if `node_modules/` exists. If missing, runs `npm ci` first.
- **Output**: Each concurrent process gets a colored label prefix (e.g. `[BlockRestrictions]`, `[Extensions]`, `[StaticAssets]`) so output is distinguishable.
- **Exit codes**: If any build fails, the script exits with a non-zero code. In dev mode, if a backoffice build fails, the Vite dev server does not start.
- **No dependencies**: Uses only Node.js built-ins (`child_process`, `fs`, `path`, `readline`).

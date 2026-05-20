#!/usr/bin/env node

import { spawn } from "node:child_process";
import { copyFileSync, createWriteStream, existsSync, statSync } from "node:fs";
import { mkdir, rename, copyFile, unlink, open, readFile, writeFile } from "node:fs/promises";
import { createInterface } from "node:readline";
import { resolve, dirname } from "node:path";
import { fileURLToPath } from "node:url";
import { pipeline } from "node:stream/promises";
import { Readable, PassThrough } from "node:stream";

const ROOT = dirname(fileURLToPath(import.meta.url));

const WEB_UI = resolve(ROOT, "src/UmbracoCommunity.Web.UI");
const SEED_URL_ENV = "IMPORT_ON_STARTUP_URL";
const SEED_FILE_ENV = "IMPORT_ON_STARTUP_FILE";
const SEED_TARGET = resolve(WEB_UI, "umbraco/Deploy/import-on-startup.zip");
const SEED_STAGE = `${SEED_TARGET}.staging`;
const DEPLOY_DIR = resolve(WEB_UI, "umbraco/Deploy");
const DEPLOY_START_MARKER = resolve(DEPLOY_DIR, "deploy-on-start");
const DEPLOY_PROGRESS_MARKER = resolve(DEPLOY_DIR, "deploy-progress");
const DEPLOY_COMPLETE_MARKER = resolve(DEPLOY_DIR, "deploy-complete");
const DEPLOY_FAILED_MARKER = resolve(DEPLOY_DIR, "deploy-failed");
const SQLITE_DB = resolve(WEB_UI, "umbraco/Data/Umbraco.sqlite.db");

// Phase 1 (schema sync) waits for the deploy-complete marker to appear.
// Configurable for slow CI / large schemas; 10 minutes is comfortable for a
// typical dev box.
const PHASE1_TIMEOUT_MS = Number(process.env.SCHEMA_SYNC_TIMEOUT_MS) || 10 * 60_000;
const MARKER_POLL_INTERVAL_MS = 500;

// Public snapshot served by the community site (regenerated daily by the
// SeedExportHostedService). Override per-run with IMPORT_ON_STARTUP_URL or
// IMPORT_ON_STARTUP_FILE.
const DEFAULT_SEED_URL = "https://community.umbraco.com/seed/latest.zip";

const SEED_TIMEOUT_MS = Number(process.env.IMPORT_ON_STARTUP_TIMEOUT_MS) || 30 * 60_000;
const ZIP_MAGIC = Buffer.from([0x50, 0x4b, 0x03, 0x04]);

const projects = {
  BlockRestrictions: {
    path: resolve(ROOT, "src/UmbracoCommunity.BlockRestrictions/Client"),
    color: "\x1b[36m", // cyan
  },
  Extensions: {
    path: resolve(ROOT, "src/UmbracoCommunity.Extensions/Client"),
    color: "\x1b[35m", // magenta
  },
  StaticAssets: {
    path: resolve(ROOT, "src/UmbracoCommunity.StaticAssets"),
    color: "\x1b[33m", // yellow
  },
  "Web.UI": {
    path: resolve(ROOT, "src/UmbracoCommunity.Web.UI"),
    color: "\x1b[34m", // blue
  },
};

const RESET = "\x1b[0m";
const BOLD = "\x1b[1m";
const DIM = "\x1b[2m";
const RED = "\x1b[31m";
const GREEN = "\x1b[32m";
const YELLOW = "\x1b[33m";
const CYAN = "\x1b[36m";

const MODES = ["dev", "dev:dotnet", "local", "local:dotnet", "seed", "reset", "update-packages", "update-packages:dry-run"];

const LAUNCH_PROFILE_DEV = "Kestrel [ENV: Development - default]";
const LAUNCH_PROFILE_LOCAL = "Kestrel [ENV: Local]";

function log(msg) {
  console.log(`${BOLD}${msg}${RESET}`);
}

function logError(msg) {
  console.error(`${RED}${msg}${RESET}`);
}

function ensureNodeModules(name, projectPath) {
  const nodeModules = resolve(projectPath, "node_modules");
  const installMarker = resolve(nodeModules, ".package-lock.json");
  const lockFile = resolve(projectPath, "package-lock.json");

  let reason = null;
  if (!existsSync(nodeModules)) {
    reason = "node_modules missing";
  } else if (existsSync(lockFile) && (!existsSync(installMarker) || statSync(lockFile).mtimeMs > statSync(installMarker).mtimeMs)) {
    reason = "package-lock.json is newer than installed packages";
  }

  if (reason) {
    log(`[${name}] ${reason}, running npm ci...`);
    return runProcess(name, "npm", ["ci"], projectPath);
  }
  return Promise.resolve();
}

const runningProcesses = [];

function runProcess(name, cmd, args, cwd) {
  return new Promise((resolve, reject) => {
    const project = projects[name];
    const prefix = project ? `${project.color}[${name}]${RESET} ` : "";

    // On Windows, npm/npx are .cmd batch files. Two security fixes constrain us:
    // - Node 22+ refuses to spawn .cmd directly without a shell (EINVAL,
    //   CVE-2024-27980 BatBadBut).
    // - Node 24+ deprecates passing an args array alongside shell:true (DEP0190),
    //   because args are concatenated unescaped.
    // Workaround: build a single quoted command string and pass empty args.
    // dotnet is a .exe and runs without shell on every platform.
    const isWindowsBatch = process.platform === "win32" && (cmd === "npm" || cmd === "npx");
    const quoteArg = (a) => /^[A-Za-z0-9._:=/\\-]+$/.test(a) ? a : `"${a.replace(/"/g, '\\"')}"`;
    const spawnCmd = isWindowsBatch ? [cmd, ...args.map(quoteArg)].join(" ") : cmd;
    const spawnArgs = isWindowsBatch ? [] : args;

    const proc = spawn(spawnCmd, spawnArgs, {
      cwd,
      stdio: ["ignore", "pipe", "pipe"],
      shell: isWindowsBatch,
      env: {
        ...process.env,
        FORCE_COLOR: "1",        // chalk, Vite, most Node tools
        DOTNET_SYSTEM_CONSOLE_ALLOW_ANSI_COLOR_REDIRECTION: "1", // dotnet
      },
    });

    runningProcesses.push(proc);

    proc.stdout.on("data", (data) => {
      for (const line of data.toString().split("\n")) {
        if (line) process.stdout.write(`${prefix}${line}\n`);
      }
    });
    proc.stderr.on("data", (data) => {
      for (const line of data.toString().split("\n")) {
        if (line) process.stderr.write(`${prefix}${line}\n`);
      }
    });

    proc.on("close", (code) => {
      const idx = runningProcesses.indexOf(proc);
      if (idx !== -1) runningProcesses.splice(idx, 1);
      if (code !== 0 && code !== null) {
        reject(new Error(`[${name}] exited with code ${code}`));
      } else {
        resolve();
      }
    });

    proc.on("error", reject);
  });
}

function killAll() {
  for (const proc of runningProcesses) {
    proc.kill("SIGTERM");
  }
}

process.on("SIGINT", () => {
  killAll();
  process.exit(0);
});
process.on("SIGTERM", () => {
  killAll();
  process.exit(0);
});

async function buildProject(name, script) {
  const project = projects[name];
  await ensureNodeModules(name, project.path);
  log(`[${name}] Running npm run ${script}...`);
  await runProcess(name, "npm", ["run", script], project.path);
  console.log(`${GREEN}[${name}] Done.${RESET}`);
}

async function runLocal(withDotnet) {
  const label = withDotnet ? "local:dotnet" : "local";
  log(`Mode: ${label} (building all projects for cloud deployment${withDotnet ? " + dotnet run" : ""})\n`);

  await offerFirstTimeSeed(withDotnet, LAUNCH_PROFILE_LOCAL);

  await Promise.all([
    buildProject("BlockRestrictions", "build"),
    buildProject("Extensions", "build"),
    buildProject("StaticAssets", "build:for:cloud"),
  ]);

  console.log(`\n${GREEN}${BOLD}All builds completed successfully.${RESET}`);

  ensureLocalAppSettings();

  if (withDotnet) {
    console.log(`\n${BOLD}Starting dotnet run...${RESET}\n`);
    await runProcess("Web.UI", "dotnet", ["run", "--launch-profile", LAUNCH_PROFILE_LOCAL], projects["Web.UI"].path);
  }
}

function ensureLocalAppSettings() {
  const webUiPath = projects["Web.UI"].path;
  const localPath = resolve(webUiPath, "appsettings.Local.json");
  const developmentPath = resolve(webUiPath, "appsettings.Development.json");

  if (existsSync(localPath)) return;

  if (!existsSync(developmentPath)) {
    logError(`appsettings.Local.json is missing and appsettings.Development.json was not found to copy from.`);
    return;
  }

  copyFileSync(developmentPath, localPath);
  log(`[Web.UI] appsettings.Local.json was missing — copied from appsettings.Development.json.`);
}

async function runDev(withDotnet, firstTimeIntro = null) {
  const label = withDotnet ? "dev:dotnet" : "dev";
  log(`Mode: ${label} (building backoffice extensions, then starting dev servers)\n`);

  await offerFirstTimeSeed(withDotnet, LAUNCH_PROFILE_DEV, firstTimeIntro);

  await Promise.all([
    buildProject("BlockRestrictions", "build"),
    buildProject("Extensions", "build"),
  ]);

  const servers = withDotnet ? "Vite dev server + dotnet run" : "Vite dev server";
  console.log(`\n${GREEN}${BOLD}Backoffice builds complete. Starting ${servers}...${RESET}\n`);

  const sa = projects.StaticAssets;
  await ensureNodeModules("StaticAssets", sa.path);

  const tasks = [runProcess("StaticAssets", "npm", ["run", "dev"], sa.path)];
  if (withDotnet) {
    tasks.push(runProcess("Web.UI", "dotnet", ["run", "--launch-profile", LAUNCH_PROFILE_DEV], projects["Web.UI"].path));
  }

  await Promise.all(tasks);
}

function utcTimestamp() {
  const d = new Date();
  const pad = (n) => String(n).padStart(2, "0");
  return `${d.getUTCFullYear()}${pad(d.getUTCMonth() + 1)}${pad(d.getUTCDate())}-${pad(d.getUTCHours())}${pad(d.getUTCMinutes())}${pad(d.getUTCSeconds())}Z`;
}

function hasUsableSeedSource() {
  return Boolean(process.env[SEED_FILE_ENV] || process.env[SEED_URL_ENV] || DEFAULT_SEED_URL);
}

async function assertValidZip(filePath) {
  const fd = await open(filePath, "r");
  try {
    const buf = Buffer.alloc(4);
    await fd.read(buf, 0, 4, 0);
    if (!buf.equals(ZIP_MAGIC)) {
      throw new Error(
        `Staged file is not a valid zip (first bytes: ${buf.toString("hex")}, expected ${ZIP_MAGIC.toString("hex")}). ` +
          `The source likely returned an error page or wrong content.`,
      );
    }
  } finally {
    await fd.close();
  }
}

function formatMB(bytes) {
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

async function downloadToFile(url, destPath) {
  const res = await fetch(url, { signal: AbortSignal.timeout(SEED_TIMEOUT_MS) });
  if (!res.ok) {
    throw new Error(`Download failed: HTTP ${res.status} ${res.statusText}`);
  }
  if (!res.body) {
    throw new Error("Download failed: empty response body");
  }

  const total = parseInt(res.headers.get("content-length") ?? "0", 10);
  const showProgress = process.stderr.isTTY;
  const counter = new PassThrough();
  let received = 0;
  let lastLog = 0;

  counter.on("data", (chunk) => {
    received += chunk.length;
    if (!showProgress) return;
    const now = Date.now();
    if (now - lastLog < 250) return;
    lastLog = now;
    const line = total > 0
      ? `  ${formatMB(received)} / ${formatMB(total)} (${((received / total) * 100).toFixed(1)}%)`
      : `  ${formatMB(received)}`;
    process.stderr.write(`\r${line}\x1b[K`);
  });

  await pipeline(Readable.fromWeb(res.body), counter, createWriteStream(destPath));

  if (showProgress) {
    process.stderr.write(`\r  ${formatMB(received)} downloaded.\x1b[K\n`);
  }
}

async function obtainSeedZip(targetPath = SEED_TARGET) {
  const file = process.env[SEED_FILE_ENV];
  const explicitUrl = process.env[SEED_URL_ENV];

  if (explicitUrl && file) {
    logError(`Both ${SEED_URL_ENV} and ${SEED_FILE_ENV} are set — choose one.`);
    process.exit(1);
  }

  const url = file ? null : (explicitUrl ?? DEFAULT_SEED_URL);

  if (!url && !file) {
    logError("No seed source available.");
    logError(`Set ${SEED_URL_ENV} or ${SEED_FILE_ENV}, e.g.:`);
    logError(`  ${SEED_URL_ENV}=https://example.blob.core.windows.net/seed/import-on-startup.latest.zip`);
    logError(`  ${SEED_FILE_ENV}=./path/to/local/import-on-startup.zip`);
    process.exit(1);
  }

  const targetDir = dirname(targetPath);
  if (!existsSync(targetDir)) {
    await mkdir(targetDir, { recursive: true });
  }

  // Atomic write: stage at .partial, rename on success, clean up on failure.
  // Guarantees the target is either fully valid or unchanged — Deploy never
  // sees a half-written zip.
  const stage = `${targetPath}.partial`;
  if (existsSync(stage)) await unlink(stage);

  try {
    if (file) {
      const source = resolve(file);
      if (!existsSync(source)) {
        logError(`File not found: ${source}`);
        process.exit(1);
      }
      log(`Copying seed zip from ${source}`);
      await copyFile(source, stage);
    } else {
      log(`Downloading seed zip from ${url}`);
      await downloadToFile(url, stage);
    }
    await assertValidZip(stage);
    await rename(stage, targetPath);
  } catch (err) {
    if (existsSync(stage)) await unlink(stage).catch(() => {});
    throw err;
  }

  console.log(`${GREEN}Seed zip written to ${targetPath}${RESET}`);
}

async function backupSqlite() {
  const ts = utcTimestamp();
  const candidates = [SQLITE_DB, `${SQLITE_DB}-shm`, `${SQLITE_DB}-wal`];
  let renamed = 0;
  for (const src of candidates) {
    if (!existsSync(src)) continue;
    const dest = `${src}.bak-${ts}`;
    try {
      await rename(src, dest);
      console.log(`Renamed ${src} -> ${dest}`);
      renamed++;
    } catch (err) {
      if (err.code === "EBUSY" || err.code === "EPERM" || err.code === "EACCES") {
        logError(`Cannot rename ${src}: file is locked.`);
        logError("Stop any running 'dotnet run' instance and try again.");
        process.exit(1);
      }
      throw err;
    }
  }
  if (renamed === 0) {
    log("No existing SQLite DB to back up — Umbraco will install fresh on next boot.");
  }
}

async function promptYesNo(question, defaultYes = true) {
  if (!process.stdin.isTTY) return false;
  const rl = createInterface({ input: process.stdin, output: process.stdout });
  return new Promise((resolve) => {
    const suffix = defaultYes ? " (Y/n) " : " (y/N) ";
    rl.question(`${BOLD}${question}${RESET}${suffix}`, (answer) => {
      rl.close();
      const trimmed = answer.trim().toLowerCase();
      if (!trimmed) return resolve(defaultYes);
      resolve(trimmed === "y" || trimmed === "yes");
    });
  });
}

async function writeDeployStartMarker() {
  // Clear all prior marker state so we can cleanly detect the new sync's outcome.
  // deploy-complete: from a previous successful sync — must be removed so we don't
  //   confuse stale success with a fresh one.
  // deploy-progress / deploy-failed: from a crashed previous boot — would otherwise
  //   block Deploy from accepting a new sync request.
  for (const stale of [DEPLOY_COMPLETE_MARKER, DEPLOY_PROGRESS_MARKER, DEPLOY_FAILED_MARKER]) {
    if (existsSync(stale)) await unlink(stale).catch(() => {});
  }
  if (!existsSync(DEPLOY_DIR)) await mkdir(DEPLOY_DIR, { recursive: true });
  await writeFile(DEPLOY_START_MARKER, "");
}

async function waitForSchemaSync(timeoutMs) {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    if (existsSync(DEPLOY_COMPLETE_MARKER)) return "complete";
    if (existsSync(DEPLOY_FAILED_MARKER)) return "failed";
    await new Promise((r) => setTimeout(r, MARKER_POLL_INTERVAL_MS));
  }
  return "timeout";
}

async function readDeployFailedMessage() {
  try {
    const contents = await readFile(DEPLOY_FAILED_MARKER, "utf8");
    return contents.trim() || "(deploy-failed file was empty)";
  } catch {
    return "(no error details available)";
  }
}

async function runPhase1SchemaSync(launchProfile) {
  log("[Phase 1/2] Starting Umbraco to install the database and sync schema from git.");
  log("[Phase 1/2] Watching for deploy-complete marker. This typically takes 30-90 seconds.\n");

  const dotnetTask = runProcess(
    "Web.UI",
    "dotnet",
    ["run", "--launch-profile", launchProfile],
    projects["Web.UI"].path,
  );
  // dotnet will be killed by signal once schema sync completes — swallow that
  // non-zero exit so it doesn't surface as an unhandled rejection.
  dotnetTask.catch(() => {});

  const outcome = await Promise.race([
    waitForSchemaSync(PHASE1_TIMEOUT_MS),
    dotnetTask.then(() => "exited", () => "exited"),
  ]);

  killAll();
  // Give dotnet a moment to release file locks before phase 2 boots.
  await new Promise((r) => setTimeout(r, 3000));

  if (outcome === "complete") {
    log("\n[Phase 1/2] Schema sync complete. Proceeding to Phase 2.\n");
    return;
  }
  if (outcome === "failed") {
    const detail = await readDeployFailedMessage();
    throw new Error(`Schema sync reported failure (deploy-failed marker):\n${detail}`);
  }
  if (outcome === "timeout") {
    throw new Error(
      `Schema sync did not complete within ${Math.round(PHASE1_TIMEOUT_MS / 60_000)} minute(s). ` +
        `Increase SCHEMA_SYNC_TIMEOUT_MS if your machine needs longer.`,
    );
  }
  throw new Error("dotnet exited before schema sync completed. Check the dotnet output above for the cause.");
}

async function activateStagedSeedZip() {
  if (!existsSync(SEED_STAGE)) return;
  if (existsSync(SEED_TARGET)) await unlink(SEED_TARGET).catch(() => {});
  await rename(SEED_STAGE, SEED_TARGET);
  log(`[Phase 2/2] Seed zip moved into place at ${SEED_TARGET} — next dotnet boot will import it.`);
}

async function offerFirstTimeSeed(willStartDotnet, launchProfile, intro = null) {
  if (existsSync(SQLITE_DB)) return;

  if (!hasUsableSeedSource()) {
    log(`No Umbraco database found. To seed from a snapshot, set ${SEED_URL_ENV} or ${SEED_FILE_ENV} and run \`node build.mjs seed\`.`);
    return;
  }

  if (!process.stdin.isTTY) return;

  log(intro ?? "No Umbraco database found.");
  if (willStartDotnet) {
    log("First-time setup will:");
    log("  Phase 1: install Umbraco and sync the schema from git (~30-90s, dotnet boots once and is then stopped)");
    log("  Phase 2: import the latest community content snapshot (dotnet boots again, this time for real)");
  } else {
    log("This mode doesn't start dotnet, so I can only stage the snapshot for you.");
    log("After this completes, run `node build.mjs dev:dotnet` to finish first-time setup.");
  }

  const yes = await promptYesNo("Download the latest community snapshot and run setup now?");
  if (!yes) return;

  // Phase 1 must boot with the live target empty — any zip Deploy can see will
  // be imported against an unsynced schema and fail. Move any pre-existing
  // target zip aside (or reuse it as the stage) so we never re-download for no
  // reason after a previous partial run.
  if (existsSync(SEED_TARGET)) {
    if (existsSync(SEED_STAGE)) {
      await unlink(SEED_TARGET).catch(() => {});
    } else {
      await rename(SEED_TARGET, SEED_STAGE);
    }
  }

  if (existsSync(SEED_STAGE)) {
    log(`Reusing previously staged snapshot at ${SEED_STAGE}.`);
  } else {
    await obtainSeedZip(SEED_STAGE);
  }

  if (!willStartDotnet) {
    log(`Snapshot staged. Run \`node build.mjs dev:dotnet\` to complete setup (schema sync + content import).`);
    return;
  }

  await writeDeployStartMarker();
  await runPhase1SchemaSync(launchProfile);
  await activateStagedSeedZip();
}

async function runSeed() {
  log("Mode: seed (place latest import-on-startup.zip)\n");
  if (!existsSync(SQLITE_DB)) {
    logError("No Umbraco database found.");
    logError("For first-time setup, run `node build.mjs dev:dotnet` instead — it orchestrates schema sync (from git) and then content import (from the snapshot).");
    process.exit(1);
  }
  await obtainSeedZip(SEED_TARGET);
  log("Next dotnet boot will pick up the zip via Umbraco Deploy's ImportOnStartup.");
}

async function runUpdatePackages(dryRun) {
  const label = dryRun ? "update-packages:dry-run" : "update-packages";
  log(`Mode: ${label} (updating NuGet versions in Directory.Packages.props)\n`);
  const args = ["run", "--project", "tools/upgrade-umbraco", "--", "update-packages"];
  if (dryRun) args.push("--dry-run");
  await runProcess("upgrade-umbraco", "dotnet", args, ROOT);
  console.log(`\n${GREEN}${BOLD}Done.${RESET}`);
}

async function runReset() {
  log("Mode: reset (back up SQLite DB + run first-time setup)\n");
  await backupSqlite();
  // Backup leaves us in the no-DB state. Fall straight into dev:dotnet so the
  // orchestrated seed flow (phase 1 schema sync + phase 2 content import) runs
  // and dev servers come up — same single-command UX as a clean first boot.
  // Override the "No Umbraco database found" line so the user sees why the
  // first-time setup is running (because reset moved the DB aside, not because
  // they're a true first-timer).
  await runDev(true, "Database backed up. Running first-time setup to install a fresh DB and import the latest community content snapshot.");
}

function printBanner() {
  // A small "UCS" block in colour, framed in a rounded box. Shown only on the
  // interactive menu — direct CLI invocations stay quiet.
  const art = [
    " _   _  ___ ___ ",
    "| | | |/ __/ __|",
    "| |_| | (__\\__ \\",
    " \\___/ \\___|___/",
  ];
  const labels = [
    "",
    "Umbraco",
    "Community Site",
    "build.mjs",
  ];
  const artW = Math.max(...art.map((l) => l.length));
  const labW = Math.max(...labels.map((l) => l.length));
  const innerW = artW + 3 + labW; // 3-space gap between art and labels
  const hr = "─".repeat(innerW + 4);
  console.log("");
  console.log(`${CYAN}╭${hr}╮${RESET}`);
  for (let i = 0; i < art.length; i++) {
    const left = `${YELLOW}${art[i].padEnd(artW)}${RESET}`;
    const right = i === 0 ? labels[i].padEnd(labW) : `${BOLD}${labels[i].padEnd(labW)}${RESET}`;
    console.log(`${CYAN}│${RESET}  ${left}   ${right}  ${CYAN}│${RESET}`);
  }
  console.log(`${CYAN}╰${hr}╯${RESET}`);
  console.log("");
}

async function promptMode(showAdvanced) {
  printBanner();
  const rl = createInterface({ input: process.stdin, output: process.stdout });
  const dbExists = existsSync(SQLITE_DB);

  // Menu order: dev:dotnet (recommended, the everyday "everything starts" mode)
  // is first so a blank Enter picks it. dev is second (for people who want to
  // drive dotnet themselves). seed/reset only show when a DB exists — they
  // assume a working install. Advanced cloud-build modes are gated behind
  // --advanced. Direct CLI mode names (e.g. `node build.mjs seed`) always work.
  const options = [
    {
      key: "dev:dotnet",
      label: "dev:dotnet (recommended)",
      summary: "Compile backoffice extensions, start the Vite dev server, and run Umbraco.",
      detail: dbExists
        ? "The everyday \"everything runs\" mode."
        : "First run installs a fresh Umbraco DB and downloads the latest community sample content.",
    },
    {
      key: "dev",
      label: "dev",
      summary: "Compile backoffice extensions and start the Vite dev server only.",
      detail: "You start dotnet yourself in another terminal (or via your IDE). Useful when you want to attach a debugger or manage the Umbraco process directly.",
    },
  ];

  if (dbExists) {
    options.push({
      key: "seed",
      label: "seed",
      summary: "Refresh sample content from the latest community snapshot.",
      detail: "Downloads the snapshot zip; the next dotnet boot picks it up and imports it via Umbraco Deploy's ImportOnStartup.",
    });
    options.push({
      key: "reset",
      label: "reset",
      summary: "Back up the local database and re-run first-time setup.",
      detail: "Renames Umbraco.sqlite.db aside (timestamped backup), then orchestrates schema sync + content import + dev servers — same as a clean first boot.",
    });
  }

  if (showAdvanced) {
    options.push(
      {
        key: "local",
        label: "local (advanced)",
        summary: "Cloud-shaped build: all three frontend projects via their production scripts, no dev servers.",
        detail: "Used to validate the cloud deployment build locally.",
      },
      {
        key: "local:dotnet",
        label: "local:dotnet (advanced)",
        summary: "Cloud-shaped build + dotnet run.",
        detail: "Full cloud-shape boot for end-to-end testing.",
      },
      {
        key: "update-packages:dry-run",
        label: "update-packages:dry-run (advanced)",
        summary: "Preview NuGet package updates in Directory.Packages.props.",
        detail: "Runs the upgrade-umbraco tool in dry-run mode — shows what would change without modifying any files.",
      },
      {
        key: "update-packages",
        label: "update-packages (advanced)",
        summary: "Apply NuGet package updates to Directory.Packages.props.",
        detail: "Runs the upgrade-umbraco tool to bump every package to its latest stable version.",
      },
    );
  }

  const lines = options.map((opt, i) => {
    // Split off a trailing "(...)" tag from the label so we can colour it
    // separately: green for recommended, dim for advanced.
    const tagMatch = opt.label.match(/^(.*?)\s+\((.*)\)$/);
    const head = tagMatch ? tagMatch[1] : opt.label;
    const tag = tagMatch ? tagMatch[2] : null;
    const tagColor = tag === "recommended" ? GREEN : DIM;
    const tagText = tag ? ` ${tagColor}(${tag})${RESET}` : "";
    return (
      `  ${YELLOW}${BOLD}${i + 1})${RESET} ${BOLD}${head}${RESET}${tagText}\n` +
      `     ${opt.summary}\n` +
      `     ${DIM}${opt.detail}${RESET}`
    );
  });
  const map = Object.fromEntries(options.map((opt, i) => [String(i + 1), opt.key]));

  return new Promise((resolve) => {
    rl.question(
      `${CYAN}${BOLD}Select build mode:${RESET}\n\n` +
        lines.join("\n\n") +
        `\n\n${BOLD}Choice${RESET} [1-${options.length}] ${DIM}(Enter for ${options[0].key})${RESET}: `,
      (answer) => {
        rl.close();
        const trimmed = answer.trim().toLowerCase();
        if (!trimmed) return resolve(options[0].key);
        if (map[trimmed]) return resolve(map[trimmed]);
        if (MODES.includes(trimmed)) return resolve(trimmed);
        resolve(options[0].key);
      },
    );
  });
}

async function main() {
  const args = process.argv.slice(2).map((a) => a.toLowerCase());
  const showAdvanced = args.includes("--advanced") || args.includes("-a");
  const arg = args.find((a) => !a.startsWith("-"));
  let mode;

  if (arg && MODES.includes(arg)) {
    mode = arg;
  } else if (arg) {
    logError(`Unknown mode: ${arg}`);
    console.log(`Usage: node build.mjs [${MODES.join("|")}] [--advanced]\n`);
    process.exit(1);
  } else {
    mode = await promptMode(showAdvanced);
  }

  if (mode === "seed") {
    await runSeed();
    return;
  }
  if (mode === "reset") {
    await runReset();
    return;
  }
  if (mode === "update-packages" || mode === "update-packages:dry-run") {
    await runUpdatePackages(mode === "update-packages:dry-run");
    return;
  }

  const withDotnet = mode.endsWith(":dotnet");

  if (mode.startsWith("local")) {
    await runLocal(withDotnet);
  } else {
    await runDev(withDotnet);
  }
}

main().catch((err) => {
  logError(err.message);
  process.exit(1);
});

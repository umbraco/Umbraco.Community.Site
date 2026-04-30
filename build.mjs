#!/usr/bin/env node

import { spawn } from "node:child_process";
import { copyFileSync, existsSync, createWriteStream } from "node:fs";
import { mkdir, rename, copyFile, unlink } from "node:fs/promises";
import { createInterface } from "node:readline";
import { resolve, dirname } from "node:path";
import { fileURLToPath } from "node:url";
import { pipeline } from "node:stream/promises";
import { Readable } from "node:stream";

const ROOT = dirname(fileURLToPath(import.meta.url));

const WEB_UI = resolve(ROOT, "src/UmbracoCommunity.Web.UI");
const SEED_URL_ENV = "IMPORT_ON_STARTUP_URL";
const SEED_FILE_ENV = "IMPORT_ON_STARTUP_FILE";
const SEED_TARGET = resolve(WEB_UI, "umbraco/Deploy/import-on-startup.zip");
const SQLITE_DB = resolve(WEB_UI, "umbraco/Data/Umbraco.sqlite.db");

// TODO: set to the public blob URL once provisioned, e.g.
// "https://example.blob.core.windows.net/seed/import-on-startup.latest.zip"
// While this is null, contributors must set IMPORT_ON_STARTUP_URL or
// IMPORT_ON_STARTUP_FILE explicitly, and the first-run prompt is suppressed.
const DEFAULT_SEED_URL = null;

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
const RED = "\x1b[31m";
const GREEN = "\x1b[32m";

const MODES = ["dev", "dev:dotnet", "local", "local:dotnet", "seed", "reset"];

function log(msg) {
  console.log(`${BOLD}${msg}${RESET}`);
}

function logError(msg) {
  console.error(`${RED}${msg}${RESET}`);
}

function ensureNodeModules(name, projectPath) {
  if (!existsSync(resolve(projectPath, "node_modules"))) {
    log(`[${name}] node_modules missing, running npm ci...`);
    return runProcess(name, "npm", ["ci"], projectPath);
  }
  return Promise.resolve();
}

const runningProcesses = [];

function runProcess(name, cmd, args, cwd) {
  return new Promise((resolve, reject) => {
    const project = projects[name];
    const prefix = project ? `${project.color}[${name}]${RESET} ` : "";

    // On Windows, npm/npx are .cmd files and need a shell. dotnet is a .exe
    // and must NOT use a shell — passing it through cmd.exe mangles args that
    // contain spaces + brackets (e.g. the "Kestrel [ENV: Local]" profile name).
    const needsShell = process.platform === "win32" && (cmd === "npm" || cmd === "npx");

    const proc = spawn(cmd, args, {
      cwd,
      stdio: ["ignore", "pipe", "pipe"],
      shell: needsShell,
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

  await offerFirstTimeSeed();

  await Promise.all([
    buildProject("BlockRestrictions", "build"),
    buildProject("Extensions", "build"),
    buildProject("StaticAssets", "build:for:cloud"),
  ]);

  console.log(`\n${GREEN}${BOLD}All builds completed successfully.${RESET}`);

  ensureLocalAppSettings();

  if (withDotnet) {
    console.log(`\n${BOLD}Starting dotnet run...${RESET}\n`);
    await runProcess("Web.UI", "dotnet", ["run", "--launch-profile", "Kestrel [ENV: Local]"], projects["Web.UI"].path);
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

async function runDev(withDotnet) {
  const label = withDotnet ? "dev:dotnet" : "dev";
  log(`Mode: ${label} (building backoffice extensions, then starting dev servers)\n`);

  await offerFirstTimeSeed();

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
    tasks.push(runProcess("Web.UI", "dotnet", ["run", "--launch-profile", "Kestrel [ENV: Development - default]"], projects["Web.UI"].path));
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

async function obtainSeedZip() {
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

  const targetDir = dirname(SEED_TARGET);
  if (!existsSync(targetDir)) {
    await mkdir(targetDir, { recursive: true });
  }

  // Atomic write: stage at .partial, rename on success, clean up on failure.
  // Guarantees the target is either fully valid or unchanged — Deploy never
  // sees a half-written zip.
  const stage = `${SEED_TARGET}.partial`;
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
      const res = await fetch(url);
      if (!res.ok) {
        throw new Error(`Download failed: HTTP ${res.status} ${res.statusText}`);
      }
      if (!res.body) {
        throw new Error("Download failed: empty response body");
      }
      await pipeline(Readable.fromWeb(res.body), createWriteStream(stage));
    }
    await rename(stage, SEED_TARGET);
  } catch (err) {
    if (existsSync(stage)) await unlink(stage).catch(() => {});
    throw err;
  }

  console.log(`${GREEN}Seed zip written to ${SEED_TARGET}${RESET}`);
  console.log(`${BOLD}Next: run \`node build.mjs dev:dotnet\` — Umbraco Deploy will import the zip on first boot and delete it on success.${RESET}`);
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

async function offerFirstTimeSeed() {
  if (existsSync(SQLITE_DB)) return;

  if (!hasUsableSeedSource()) {
    log(`No Umbraco database found. To seed from a snapshot, set ${SEED_URL_ENV} or ${SEED_FILE_ENV} and run \`node build.mjs seed\`.`);
    return;
  }

  if (!process.stdin.isTTY) return;

  const yes = await promptYesNo("No Umbraco database found. Seed from the latest community snapshot before starting?");
  if (yes) {
    await runSeed();
  }
}

async function runSeed() {
  log("Mode: seed (place latest import-on-startup.zip)\n");
  await obtainSeedZip();
}

async function runReset() {
  log("Mode: reset (back up SQLite DB + place latest import-on-startup.zip)\n");
  await backupSqlite();
  await obtainSeedZip();
}

async function promptMode() {
  const rl = createInterface({ input: process.stdin, output: process.stdout });
  return new Promise((resolve) => {
    rl.question(
      `${BOLD}Select build mode:${RESET}\n` +
        `  1) dev          - Build backoffice + start Vite dev server\n` +
        `  2) dev:dotnet   - Build backoffice + start Vite dev server + dotnet run\n` +
        `  3) local        - Build all projects for cloud deployment\n` +
        `  4) local:dotnet - Build all projects for cloud + dotnet run\n` +
        `  5) seed         - Place latest import-on-startup.zip into umbraco/Deploy/\n` +
        `  6) reset        - Back up SQLite DB, then place latest import-on-startup.zip\n` +
        `\nChoice [1-6]: `,
      (answer) => {
        rl.close();
        const trimmed = answer.trim().toLowerCase();
        const map = { "1": "dev", "2": "dev:dotnet", "3": "local", "4": "local:dotnet", "5": "seed", "6": "reset" };
        resolve(map[trimmed] || (MODES.includes(trimmed) ? trimmed : "dev"));
      }
    );
  });
}

async function main() {
  const arg = process.argv[2]?.toLowerCase();
  let mode;

  if (MODES.includes(arg)) {
    mode = arg;
  } else if (arg) {
    logError(`Unknown mode: ${arg}`);
    console.log(`Usage: node build.mjs [${MODES.join("|")}]\n`);
    process.exit(1);
  } else {
    mode = await promptMode();
  }

  if (mode === "seed") {
    await runSeed();
    return;
  }
  if (mode === "reset") {
    await runReset();
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

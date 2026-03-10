#!/usr/bin/env node

import { spawn } from "node:child_process";
import { existsSync } from "node:fs";
import { createInterface } from "node:readline";
import { resolve, dirname } from "node:path";
import { fileURLToPath } from "node:url";

const ROOT = dirname(fileURLToPath(import.meta.url));

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

const MODES = ["dev", "dev:dotnet", "local", "local:dotnet"];

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

    const proc = spawn(cmd, args, {
      cwd,
      stdio: ["ignore", "pipe", "pipe"],
      shell: process.platform === "win32",
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

  await Promise.all([
    buildProject("BlockRestrictions", "build"),
    buildProject("Extensions", "build"),
    buildProject("StaticAssets", "build:for:cloud"),
  ]);

  console.log(`\n${GREEN}${BOLD}All builds completed successfully.${RESET}`);

  if (withDotnet) {
    console.log(`\n${BOLD}Starting dotnet run...${RESET}\n`);
    await runProcess("Web.UI", "dotnet", ["run"], projects["Web.UI"].path);
  }
}

async function runDev(withDotnet) {
  const label = withDotnet ? "dev:dotnet" : "dev";
  log(`Mode: ${label} (building backoffice extensions, then starting dev servers)\n`);

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
    tasks.push(runProcess("Web.UI", "dotnet", ["run"], projects["Web.UI"].path));
  }

  await Promise.all(tasks);
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
        `\nChoice [1-4]: `,
      (answer) => {
        rl.close();
        const trimmed = answer.trim().toLowerCase();
        const map = { "1": "dev", "2": "dev:dotnet", "3": "local", "4": "local:dotnet" };
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

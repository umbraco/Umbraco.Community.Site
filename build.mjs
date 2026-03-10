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
};

const RESET = "\x1b[0m";
const BOLD = "\x1b[1m";
const RED = "\x1b[31m";
const GREEN = "\x1b[32m";

function log(msg) {
  console.log(`${BOLD}${msg}${RESET}`);
}

function logError(msg) {
  console.error(`${RED}${msg}${RESET}`);
}

function ensureNodeModules(name, projectPath) {
  if (!existsSync(resolve(projectPath, "node_modules"))) {
    log(`[${name}] node_modules missing, running npm ci...`);
    return runProcess(name, "npm", ["ci"], projectPath, true);
  }
  return Promise.resolve();
}

function runProcess(name, cmd, args, cwd, pipe = true) {
  return new Promise((resolve, reject) => {
    const project = projects[name];
    const prefix = project ? `${project.color}[${name}]${RESET} ` : "";

    const proc = spawn(cmd, args, {
      cwd,
      stdio: pipe ? ["ignore", "pipe", "pipe"] : "inherit",
      shell: process.platform === "win32",
    });

    if (pipe) {
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
    }

    proc.on("close", (code) => {
      if (code !== 0) {
        reject(new Error(`[${name}] exited with code ${code}`));
      } else {
        resolve();
      }
    });

    proc.on("error", reject);
  });
}

function runForeground(name, cmd, args, cwd) {
  return new Promise((resolve, reject) => {
    const proc = spawn(cmd, args, {
      cwd,
      stdio: "inherit",
      shell: process.platform === "win32",
    });

    proc.on("close", (code) => {
      if (code !== null && code !== 0) {
        reject(new Error(`[${name}] exited with code ${code}`));
      } else {
        resolve();
      }
    });

    proc.on("error", reject);
  });
}

async function buildProject(name, script) {
  const project = projects[name];
  await ensureNodeModules(name, project.path);
  log(`[${name}] Running npm run ${script}...`);
  await runProcess(name, "npm", ["run", script], project.path);
  console.log(`${GREEN}[${name}] Done.${RESET}`);
}

async function runLocal() {
  log("Mode: local (building all projects for cloud deployment)\n");

  await Promise.all([
    buildProject("BlockRestrictions", "build"),
    buildProject("Extensions", "build"),
    buildProject("StaticAssets", "build:for:cloud"),
  ]);

  console.log(`\n${GREEN}${BOLD}All builds completed successfully.${RESET}`);
}

async function runDev() {
  log("Mode: dev (building backoffice extensions, then starting Vite dev server)\n");

  await Promise.all([
    buildProject("BlockRestrictions", "build"),
    buildProject("Extensions", "build"),
  ]);

  console.log(`\n${GREEN}${BOLD}Backoffice builds complete. Starting Vite dev server...${RESET}\n`);

  const sa = projects.StaticAssets;
  await ensureNodeModules("StaticAssets", sa.path);
  await runForeground("StaticAssets", "npm", ["run", "dev"], sa.path);
}

async function promptMode() {
  const rl = createInterface({ input: process.stdin, output: process.stdout });
  return new Promise((resolve) => {
    rl.question(
      `${BOLD}Select build mode:${RESET}\n  1) dev   - Build backoffice + start Vite dev server\n  2) local - Build all projects for cloud deployment\n\nChoice [1/2]: `,
      (answer) => {
        rl.close();
        const trimmed = answer.trim().toLowerCase();
        if (trimmed === "2" || trimmed === "local") resolve("local");
        else resolve("dev");
      }
    );
  });
}

async function main() {
  const arg = process.argv[2]?.toLowerCase();
  let mode;

  if (arg === "local" || arg === "dev") {
    mode = arg;
  } else if (arg) {
    logError(`Unknown mode: ${arg}`);
    console.log(`Usage: node build.mjs [dev|local]\n`);
    process.exit(1);
  } else {
    mode = await promptMode();
  }

  if (mode === "local") {
    await runLocal();
  } else {
    await runDev();
  }
}

main().catch((err) => {
  logError(err.message);
  process.exit(1);
});

// Generates docs/contributors.generated.json from git history, enriched with GitHub
// avatars where available.
//
// For every documentation article (docs/tutorials/**, docs/primers/**) it reads the
// per-file git log — following renames — for the commit authors and any real
// Co-authored-by people (oldest contribution first; AI/bot identities filtered). When a
// GitHub token is present (CI) it also calls the commits API to map each author's email
// to their GitHub account, attaching `login`, `avatarUrl`, and `profileUrl`. Without a
// token (or for authors GitHub can't match) entries are name-only — the view falls back
// to a name chip. This must run at build/CI time; the deployed app has no .git.
//
// Run via `npm run generate:doc-contributors`.

import { execFileSync } from "node:child_process";
import { writeFileSync, readdirSync, statSync } from "node:fs";
import { join, relative, sep } from "node:path";

const US = "\x1f"; // field separator
const RS = "\x1e"; // co-author separator

const EMAIL_DENYLIST = [/^noreply@anthropic\.com$/i];
const NAME_DENYLIST = [/\[bot\]$/i, /^dependabot/i, /^github-actions/i];

const SURFACED = ["tutorials", "primers"];
const EXCLUDED_FILES = new Set(["IDEAS.md"]);

function git(args) {
  return execFileSync("git", args, { encoding: "utf8", maxBuffer: 64 * 1024 * 1024 });
}

const repoRoot = git(["rev-parse", "--show-toplevel"]).trim();
const docsRoot = join(repoRoot, "docs");

const token = process.env.GITHUB_TOKEN || process.env.GH_TOKEN || "";
const repoSlug = resolveRepoSlug();

function resolveRepoSlug() {
  if (process.env.GITHUB_REPOSITORY) return process.env.GITHUB_REPOSITORY;
  try {
    const remote = git(["remote", "get-url", "origin"]).trim();
    const match = remote.match(/github\.com[:/](.+?)(?:\.git)?$/i);
    return match ? match[1] : null;
  } catch {
    return null;
  }
}

function walkMarkdown(dir) {
  const out = [];
  for (const entry of readdirSync(dir)) {
    const full = join(dir, entry);
    if (statSync(full).isDirectory()) {
      out.push(...walkMarkdown(full));
    } else if (entry.toLowerCase().endsWith(".md") && !EXCLUDED_FILES.has(entry)) {
      out.push(full);
    }
  }
  return out;
}

function isAllowed(name, email) {
  if (!name && !email) return false;
  if (EMAIL_DENYLIST.some((re) => re.test(email))) return false;
  if (NAME_DENYLIST.some((re) => re.test(name))) return false;
  return true;
}

// Ordered, de-duplicated [{ name, email }] for a file, oldest contribution first.
function gitContributors(absPath) {
  const format = "%x00%an%x1f%ae%x1f%(trailers:key=Co-authored-by,valueonly,separator=%x1e)";
  const log = git(["log", "--follow", "--no-merges", `--pretty=format:${format}`, "--", absPath]);
  const commits = log.split("\x00").filter((c) => c.length > 0).reverse();

  const byEmail = new Map();
  const order = [];
  const add = (name, email) => {
    const trimmedName = (name || "").trim();
    const key = (email || "").trim().toLowerCase();
    if (!isAllowed(trimmedName, key) || byEmail.has(key)) return;
    byEmail.set(key, { name: trimmedName, email: key });
    order.push(key);
  };

  for (const commit of commits) {
    const [name, email, coauthors] = commit.split(US);
    add(name, email);
    if (coauthors) {
      for (const entry of coauthors.split(RS)) {
        const match = entry.match(/^\s*(.*?)\s*<(.+?)>\s*$/);
        if (match) add(match[1], match[2]);
      }
    }
  }

  return order.map((email) => byEmail.get(email));
}

// email (lowercased) -> { login, avatarUrl, profileUrl } from the GitHub commits API.
async function githubAvatarsForPath(relPath) {
  if (!token || !repoSlug) return new Map();

  const map = new Map();
  let page = 1;
  try {
    for (;;) {
      const url =
        `https://api.github.com/repos/${repoSlug}/commits` +
        `?path=${encodeURIComponent(relPath)}&per_page=100&page=${page}`;
      const res = await fetch(url, {
        headers: {
          Authorization: `Bearer ${token}`,
          Accept: "application/vnd.github+json",
          "User-Agent": "generate-doc-contributors",
        },
      });
      if (!res.ok) {
        console.warn(`GitHub API ${res.status} for ${relPath}; falling back to name-only.`);
        break;
      }
      const batch = await res.json();
      for (const commit of batch) {
        const email = commit?.commit?.author?.email?.toLowerCase();
        const user = commit?.author;
        if (!email || !user || user.type === "Bot" || map.has(email)) continue;
        map.set(email, {
          login: user.login,
          avatarUrl: user.avatar_url,
          profileUrl: user.html_url,
        });
      }
      if (batch.length < 100) break;
      page++;
    }
  } catch (err) {
    console.warn(`GitHub API error for ${relPath}: ${err.message}; falling back to name-only.`);
  }
  return map;
}

const result = {};
for (const section of SURFACED) {
  const dir = join(docsRoot, section);
  let files;
  try {
    files = walkMarkdown(dir);
  } catch {
    continue;
  }
  for (const file of files) {
    const key = relative(docsRoot, file).split(sep).join("/");
    const contributors = gitContributors(file);
    if (contributors.length === 0) continue;

    const avatars = await githubAvatarsForPath(key);
    result[key] = contributors.map(({ name, email }) => {
      const gh = avatars.get(email);
      return gh
        ? { name, login: gh.login, avatarUrl: gh.avatarUrl, profileUrl: gh.profileUrl }
        : { name };
    });
  }
}

const ordered = Object.fromEntries(Object.keys(result).sort().map((k) => [k, result[k]]));
const outPath = join(docsRoot, "contributors.generated.json");
writeFileSync(outPath, JSON.stringify(ordered, null, 2) + "\n", "utf8");

const withAvatars = Object.values(ordered).flat().filter((c) => c.avatarUrl).length;
console.log(
  `Wrote ${Object.keys(ordered).length} doc entries to ${relative(repoRoot, outPath)} ` +
  `(${withAvatars} contributor(s) with avatars${token ? "" : "; no token — name-only"}).`
);

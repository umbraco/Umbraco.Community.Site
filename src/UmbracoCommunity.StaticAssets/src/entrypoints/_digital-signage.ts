import "../css/pages/digital-signage.css";

const REFRESH_INTERVAL_MS = 60_000;
const EVENT_TIMEZONE = "Europe/Copenhagen";

const overrideRaw = new URLSearchParams(window.location.search).get("signage-now") ?? "";
const parsedOverride = overrideRaw ? Date.parse(overrideRaw) : NaN;
const usingOverride = !Number.isNaN(parsedOverride);
const overrideStart = usingOverride ? parsedOverride : 0;
const pageLoadAt = Date.now();

function currentSimulatedTime(): Date {
  return usingOverride
    ? new Date(overrideStart + (Date.now() - pageLoadAt))
    : new Date();
}

function ordinalSuffix(n: number): string {
  const mod100 = n % 100;
  if (mod100 >= 11 && mod100 <= 13) return "th";
  switch (n % 10) {
    case 1: return "st";
    case 2: return "nd";
    case 3: return "rd";
    default: return "th";
  }
}

let clockInterval: number | undefined;

function bindClock() {
  if (clockInterval !== undefined) {
    window.clearInterval(clockInterval);
    clockInterval = undefined;
  }

  const root = document.querySelector<HTMLElement>("[data-signage-clock]");
  if (!root) return;
  const timeEl = root.querySelector<HTMLElement>("[data-clock-time]");
  const dateEl = root.querySelector<HTMLElement>("[data-clock-date]");
  if (!timeEl || !dateEl) return;

  let lastDateText = "";

  const tick = () => {
    const now = currentSimulatedTime();

    timeEl.textContent = now.toLocaleTimeString("en-GB", {
      timeZone: usingOverride ? undefined : EVENT_TIMEZONE,
      hour: "2-digit",
      minute: "2-digit",
      second: "2-digit",
      hour12: false,
    });

    const parts = new Intl.DateTimeFormat("en-GB", {
      timeZone: usingOverride ? undefined : EVENT_TIMEZONE,
      weekday: "short",
      day: "numeric",
      month: "long",
    }).formatToParts(now);
    const partOf = (type: Intl.DateTimeFormatPartTypes) =>
      parts.find((p) => p.type === type)?.value ?? "";
    const dayNum = parseInt(partOf("day"), 10);
    const dateText = `${partOf("weekday")} ${dayNum}${ordinalSuffix(dayNum)} ${partOf("month")}`;
    if (dateText !== lastDateText) {
      dateEl.textContent = dateText;
      lastDateText = dateText;
    }
  };

  tick();
  clockInterval = window.setInterval(tick, 1000);
}

bindClock();

function buildRefreshUrl(): string {
  if (!usingOverride) return window.location.href;
  const url = new URL(window.location.href);
  url.searchParams.set("signage-now", currentSimulatedTime().toISOString());
  return url.toString();
}

async function refreshProgramBlocks() {
  if (document.hidden) {
    return;
  }

  try {
    const response = await fetch(buildRefreshUrl(), {
      cache: "no-cache",
      headers: { "X-Signage-Refresh": "1" },
    });
    if (!response.ok) {
      console.warn("Signage refresh failed:", response.status);
      return;
    }

    const html = await response.text();
    const doc = new DOMParser().parseFromString(html, "text/html");

    const blocks = document.querySelectorAll<HTMLElement>("[data-program-block]");
    blocks.forEach((block) => {
      const blockId = block.dataset.programBlock;
      if (!blockId) return;

      const freshBlock = doc.querySelector<HTMLElement>(
        `[data-program-block="${CSS.escape(blockId)}"]`
      );
      const freshBody = freshBlock?.querySelector("[data-program-body]");
      const targetBody = block.querySelector("[data-program-body]");

      if (freshBody && targetBody) {
        targetBody.replaceWith(freshBody);
      }
    });

    bindClock();
  } catch (err) {
    console.warn("Signage refresh threw:", err);
  }
}

window.setInterval(refreshProgramBlocks, REFRESH_INTERVAL_MS);

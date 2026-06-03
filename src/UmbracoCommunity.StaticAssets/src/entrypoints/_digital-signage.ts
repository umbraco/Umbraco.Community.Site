import "../css/pages/digital-signage.css";

const REFRESH_INTERVAL_MS = 60_000;
const EVENT_TIMEZONE = "Europe/Copenhagen";

function startClock() {
  const root = document.querySelector<HTMLElement>("[data-signage-clock]");
  if (!root) return;

  const timeEl = root.querySelector<HTMLElement>("[data-clock-time]");
  const dateEl = root.querySelector<HTMLElement>("[data-clock-date]");
  if (!timeEl || !dateEl) return;

  const overrideRaw = root.dataset.signageNow ?? "";
  const overrideStart = overrideRaw ? Date.parse(overrideRaw) : NaN;
  const usingOverride = !Number.isNaN(overrideStart);
  const pageLoadAt = Date.now();

  const tick = () => {
    const now = usingOverride
      ? new Date(overrideStart + (Date.now() - pageLoadAt))
      : new Date();

    timeEl.textContent = now.toLocaleTimeString("en-GB", {
      timeZone: usingOverride ? undefined : EVENT_TIMEZONE,
      hour: "2-digit",
      minute: "2-digit",
      second: "2-digit",
      hour12: false,
    });
    dateEl.textContent = now.toLocaleDateString("en-GB", {
      timeZone: usingOverride ? undefined : EVENT_TIMEZONE,
      weekday: "long",
      day: "numeric",
      month: "long",
    });
  };

  tick();
  window.setInterval(tick, 1000);
}

startClock();

async function refreshProgramBlocks() {
  if (document.hidden) {
    return;
  }

  try {
    const response = await fetch(window.location.href, {
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
  } catch (err) {
    console.warn("Signage refresh threw:", err);
  }
}

window.setInterval(refreshProgramBlocks, REFRESH_INTERVAL_MS);

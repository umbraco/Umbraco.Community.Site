import { customElement } from "lit/decorators.js";

const elementName = "dc-event-time";

const dateFmt = new Intl.DateTimeFormat(undefined, {
  weekday: "short",
  day: "numeric",
  month: "short",
  year: "numeric",
});

const timeFmt = new Intl.DateTimeFormat(undefined, {
  hour: "2-digit",
  minute: "2-digit",
  hour12: false,
});

const tzFmt = new Intl.DateTimeFormat(undefined, { timeZoneName: "short" });

const infoIcon = `<svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><circle cx="12" cy="12" r="10"/><path d="M12 16v-4"/><path d="M12 8h.01"/></svg>`;

let popoverIdCounter = 0;

@customElement(elementName)
export class DcEventTime extends HTMLElement {
  static observedAttributes = ["start", "end"];

  connectedCallback() {
    this.#render();
  }

  attributeChangedCallback() {
    if (this.isConnected) this.#render();
  }

  #render() {
    const startAttr = this.getAttribute("start");
    const endAttr = this.getAttribute("end");
    if (!startAttr || !endAttr) return;

    const start = new Date(startAttr);
    const end = new Date(endAttr);
    if (Number.isNaN(start.getTime()) || Number.isNaN(end.getTime())) return;

    const localTzName =
      tzFmt
        .formatToParts(start)
        .find((p) => p.type === "timeZoneName")?.value ??
      Intl.DateTimeFormat().resolvedOptions().timeZone;

    const sourceOffsetMinutes = parseOffsetMinutes(startAttr);
    const sourceTzLabel = formatOffsetLabel(sourceOffsetMinutes);

    const localSameDay = dateFmt.format(start) === dateFmt.format(end);
    // Same-day: "Thu 30 Apr 2026, 18:00 — 20:00".
    // Multi-day: dates only in the main flow ("Wed 10 Jun 2026 — Thu 11 Jun 2026"); times in popover.
    const localStart = localSameDay
      ? `${dateFmt.format(start)}, ${timeFmt.format(start)}`
      : dateFmt.format(start);
    const localEnd = localSameDay
      ? timeFmt.format(end)
      : dateFmt.format(end);
    const localStartFull = `${dateFmt.format(start)}, ${timeFmt.format(start)}`;
    const localEndFull = localSameDay
      ? timeFmt.format(end)
      : `${dateFmt.format(end)}, ${timeFmt.format(end)}`;

    const sourceStart = formatAtOffset(start, sourceOffsetMinutes);
    const sourceEnd = formatAtOffset(end, sourceOffsetMinutes);
    const sourceSameDay = sourceStart.dateLabel === sourceEnd.dateLabel;
    const sourceStartText = `${sourceStart.dateLabel}, ${sourceStart.timeLabel}`;
    const sourceEndText = sourceSameDay
      ? sourceEnd.timeLabel
      : `${sourceEnd.dateLabel}, ${sourceEnd.timeLabel}`;

    const popoverId = `dc-event-time-popover-${++popoverIdCounter}`;
    this.replaceChildren();

    const startEl = document.createElement("time");
    startEl.setAttribute("datetime", startAttr);
    startEl.textContent = localStart;

    const sep = document.createElement("span");
    sep.className = "dc-upcoming-events__range-sep";
    sep.textContent = " — ";

    const endEl = document.createElement("time");
    endEl.setAttribute("datetime", endAttr);
    endEl.textContent = localEnd;

    const button = document.createElement("button");
    button.type = "button";
    button.className = "dc-event-time__info";
    button.setAttribute("aria-label", "About this event time");
    button.setAttribute("aria-controls", popoverId);
    button.setAttribute("aria-expanded", "false");
    button.innerHTML = infoIcon;
    button.addEventListener("click", (ev) => {
        ev.preventDefault();
        ev.stopPropagation();

        // Pre-position based on the button so the first paint isn't at the
        // browser's default top-left fallback. The toggle handler refines if
        // the popover would overflow the viewport.
        const rect = button.getBoundingClientRect();
        popover.style.top = `${rect.bottom + 4}px`;
        popover.style.left = `${rect.left}px`;

        popover.togglePopover();
    });

    const popover = document.createElement("div");
    popover.id = popoverId;
    popover.className = "dc-event-time__popover";
    popover.setAttribute("popover", "manual");
    popover.addEventListener("click", (ev) => ev.stopPropagation());
    popover.innerHTML = `
      <p class="dc-event-time__popover-row">
        <span class="dc-event-time__popover-label">In your timezone (${escape(localTzName)}):</span>
        <span class="dc-event-time__popover-value">${escape(localStartFull)} — ${escape(localEndFull)}</span>
      </p>
      <p class="dc-event-time__popover-row">
        <span class="dc-event-time__popover-label">Where the event is held (${escape(sourceTzLabel)}):</span>
        <span class="dc-event-time__popover-value">${escape(sourceStartText)} — ${escape(sourceEndText)}</span>
      </p>
    `;

    let openCleanup: AbortController | null = null;

    popover.addEventListener("toggle", (ev) => {
      const state = (ev as ToggleEvent).newState;
      button.setAttribute("aria-expanded", state === "open" ? "true" : "false");
      if (state === "open") {
        this.#positionPopover(button, popover);

        openCleanup?.abort();
        openCleanup = new AbortController();

        const close = () => {
          if (popover.matches(":popover-open")) popover.hidePopover();
        };

        const signal = openCleanup.signal;

        // Close on click outside the popover/trigger (we run popover="manual"
        // so the browser's light dismiss isn't doing this for us). Capture
        // phase so clicks on a sibling popover's trigger button still close
        // this one even though that button calls stopPropagation.
        document.addEventListener("click", (clickEvent) => {
          const target = clickEvent.target as Node;
          if (!popover.contains(target) && !button.contains(target)) close();
        }, { capture: true, signal });

        // Close on Escape.
        document.addEventListener("keydown", (keyEvent) => {
          if (keyEvent.key === "Escape") close();
        }, { signal });

        // Close on user scroll past a small threshold so any layout-settling
        // scroll right after open doesn't immediately dismiss the popover.
        const initialScrollY = window.scrollY;
        const initialScrollX = window.scrollX;
        window.addEventListener("scroll", () => {
          if (Math.abs(window.scrollY - initialScrollY) > 8 ||
              Math.abs(window.scrollX - initialScrollX) > 8) {
            close();
          }
        }, { passive: true, signal });
      } else {
        openCleanup?.abort();
        openCleanup = null;
      }
    });

    this.append(startEl, sep, endEl, button, popover);
  }

  #positionPopover(anchor: HTMLElement, popover: HTMLElement) {
    const anchorRect = anchor.getBoundingClientRect();
    const popRect = popover.getBoundingClientRect();
    const margin = 4;

    // Prefer below; flip above if it'd overflow viewport bottom.
    let top = anchorRect.bottom + margin;
    if (top + popRect.height > window.innerHeight - margin) {
      top = Math.max(margin, anchorRect.top - popRect.height - margin);
    }

    // Align left edge to anchor; clamp to viewport.
    let left = anchorRect.left;
    if (left + popRect.width > window.innerWidth - margin) {
      left = window.innerWidth - popRect.width - margin;
    }
    if (left < margin) left = margin;

    popover.style.top = `${top}px`;
    popover.style.left = `${left}px`;
  }
}

function parseOffsetMinutes(iso: string): number {
  const m = iso.match(/(Z|[+-]\d{2}:?\d{2})$/);
  if (!m) return 0;
  if (m[0] === "Z") return 0;
  const sign = m[0][0] === "+" ? 1 : -1;
  const digits = m[0].slice(1).replace(":", "");
  const hours = parseInt(digits.slice(0, 2), 10);
  const minutes = parseInt(digits.slice(2, 4), 10);
  return sign * (hours * 60 + minutes);
}

function formatOffsetLabel(minutes: number): string {
  if (minutes === 0) return "UTC";
  const sign = minutes < 0 ? "-" : "+";
  const abs = Math.abs(minutes);
  const h = Math.floor(abs / 60);
  const m = abs % 60;
  return m === 0 ? `UTC${sign}${h}` : `UTC${sign}${h}:${m.toString().padStart(2, "0")}`;
}

function formatAtOffset(d: Date, offsetMinutes: number): { dateLabel: string; timeLabel: string } {
  // Shift the absolute instant by the target offset, then format in UTC so the
  // displayed wall-clock matches the source timezone regardless of the visitor's TZ.
  const shifted = new Date(d.getTime() + offsetMinutes * 60 * 1000);
  const utcDateFmt = new Intl.DateTimeFormat(undefined, {
    weekday: "short",
    day: "numeric",
    month: "short",
    year: "numeric",
    timeZone: "UTC",
  });
  const utcTimeFmt = new Intl.DateTimeFormat(undefined, {
    hour: "2-digit",
    minute: "2-digit",
    hour12: false,
    timeZone: "UTC",
  });
  return {
    dateLabel: utcDateFmt.format(shifted),
    timeLabel: utcTimeFmt.format(shifted),
  };
}

function escape(s: string): string {
  return s
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");
}

declare global {
  interface HTMLElementTagNameMap {
    [elementName]: DcEventTime;
  }
}

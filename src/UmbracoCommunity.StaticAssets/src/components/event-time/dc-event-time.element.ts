import { customElement } from "lit/decorators.js";

const elementName = "dc-event-time";

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

    const localTz = Intl.DateTimeFormat().resolvedOptions().timeZone;
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

    const sameDay =
      dateFmt.format(start) === dateFmt.format(end);

    const tzPart = tzFmt
      .formatToParts(start)
      .find((p) => p.type === "timeZoneName")?.value ?? localTz;

    const startText = `${dateFmt.format(start)}, ${timeFmt.format(start)}`;
    const endText = sameDay
      ? timeFmt.format(end)
      : `${dateFmt.format(end)}, ${timeFmt.format(end)}`;

    this.replaceChildren();

    const startEl = document.createElement("time");
    startEl.setAttribute("datetime", startAttr);
    startEl.textContent = startText;

    const sep = document.createElement("span");
    sep.className = "dc-upcoming-events__range-sep";
    sep.textContent = " — ";

    const endEl = document.createElement("time");
    endEl.setAttribute("datetime", endAttr);
    endEl.textContent = endText;

    const tzEl = document.createElement("span");
    tzEl.className = "dc-upcoming-events__tz";
    tzEl.textContent = ` (${tzPart})`;

    this.append(startEl, sep, endEl, tzEl);
  }
}

declare global {
  interface HTMLElementTagNameMap {
    [elementName]: DcEventTime;
  }
}

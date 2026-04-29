import { describe, it, expect, beforeEach, afterEach } from "vitest";
import "./dc-event-time.element";

describe("dc-event-time", () => {
  let container: HTMLElement;

  beforeEach(() => {
    container = document.createElement("div");
    document.body.appendChild(container);
  });

  afterEach(() => {
    container.remove();
  });

  function makeElement(start: string, end: string): HTMLElement {
    const el = document.createElement("dc-event-time");
    el.setAttribute("start", start);
    el.setAttribute("end", end);
    el.innerHTML = "<span>fallback</span>";
    container.appendChild(el);
    return el;
  }

  it("renders two time elements when attributes are valid", () => {
    const el = makeElement(
      "2026-04-30T18:00:00+01:00",
      "2026-04-30T20:00:00+01:00"
    );
    const times = el.querySelectorAll("time");
    expect(times.length).toBe(2);
    expect(times[0].getAttribute("datetime")).toBe("2026-04-30T18:00:00+01:00");
    expect(times[1].getAttribute("datetime")).toBe("2026-04-30T20:00:00+01:00");
  });

  it("renders only the end time (no date) when same day in local TZ", () => {
    const el = makeElement(
      "2026-04-30T18:00:00+01:00",
      "2026-04-30T20:00:00+01:00"
    );
    const [, endEl] = el.querySelectorAll("time");
    expect(endEl.textContent).not.toMatch(/2026/);
  });

  it("renders date-only (no time) on each end when multi-day", () => {
    const el = makeElement(
      "2026-06-10T08:00:00+00:00",
      "2026-06-11T22:00:00+00:00"
    );
    const [startEl, endEl] = el.querySelectorAll("time");
    expect(startEl.textContent).toMatch(/2026/);
    expect(endEl.textContent).toMatch(/2026/);
    // Times must NOT appear in the main flow for multi-day; they live in the popover.
    expect(startEl.textContent).not.toMatch(/08[.:]00/);
    expect(endEl.textContent).not.toMatch(/22[.:]00/);
  });

  it("multi-day popover still includes the start and end times", () => {
    const el = makeElement(
      "2026-06-10T08:00:00+00:00",
      "2026-06-11T22:00:00+00:00"
    );
    const popover = el.querySelector(".dc-event-time__popover");
    expect(popover!.textContent).toMatch(/08[.:]00/);
    expect(popover!.textContent).toMatch(/22[.:]00/);
  });

  it("renders an info button with popover target", () => {
    const el = makeElement(
      "2026-04-30T18:00:00+01:00",
      "2026-04-30T20:00:00+01:00"
    );
    const button = el.querySelector("button.dc-event-time__info");
    expect(button).not.toBeNull();
    const popoverId = button!.getAttribute("popovertarget");
    expect(popoverId).toBeTruthy();
    const popover = el.querySelector(`#${popoverId}`);
    expect(popover).not.toBeNull();
    expect(popover!.getAttribute("popover")).toBe("auto");
  });

  it("popover shows source-timezone label and times", () => {
    const el = makeElement(
      "2026-04-30T18:00:00+01:00",
      "2026-04-30T20:00:00+01:00"
    );
    const popover = el.querySelector(".dc-event-time__popover");
    expect(popover).not.toBeNull();
    expect(popover!.textContent).toContain("Where the event is held (UTC+1)");
    expect(popover!.textContent).toContain("In your timezone");
    // Locale separator may be ':' or '.'; just check the digits show up.
    expect(popover!.textContent).toMatch(/18[.:]00/);
    expect(popover!.textContent).toMatch(/20[.:]00/);
  });

  it("popover labels UTC for zero-offset events", () => {
    const el = makeElement(
      "2026-06-10T08:00:00Z",
      "2026-06-10T09:00:00Z"
    );
    const popover = el.querySelector(".dc-event-time__popover");
    expect(popover!.textContent).toContain("Where the event is held (UTC)");
  });

  it("popover handles negative offsets (e.g. Eastern US)", () => {
    const el = makeElement(
      "2026-05-01T10:00:00-04:00",
      "2026-05-01T11:00:00-04:00"
    );
    const popover = el.querySelector(".dc-event-time__popover");
    expect(popover!.textContent).toContain("UTC-4");
    expect(popover!.textContent).toMatch(/10[.:]00/);
  });

  it("does not render time elements when attributes are missing", () => {
    const el = document.createElement("dc-event-time");
    el.innerHTML = "<span>fallback</span>";
    container.appendChild(el);
    expect(el.querySelector("time")).toBeNull();
    expect(el.querySelector("span")?.textContent).toBe("fallback");
  });

  it("leaves fallback content alone when attributes are invalid dates", () => {
    const el = document.createElement("dc-event-time");
    el.setAttribute("start", "not-a-date");
    el.setAttribute("end", "also-not-a-date");
    el.innerHTML = "<span>fallback</span>";
    container.appendChild(el);
    expect(el.querySelector("button")).toBeNull();
    expect(el.querySelector("span")?.textContent).toBe("fallback");
  });
});

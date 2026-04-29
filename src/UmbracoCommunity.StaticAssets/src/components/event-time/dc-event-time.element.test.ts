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

  it("renders only the end time (no date) when same day", () => {
    const el = makeElement(
      "2026-04-30T18:00:00+01:00",
      "2026-04-30T20:00:00+01:00"
    );
    const [, endEl] = el.querySelectorAll("time");
    // End text should not include a year — same-day rendering
    expect(endEl.textContent).not.toMatch(/2026/);
  });

  it("renders both date+time on each end when multi-day", () => {
    const el = makeElement(
      "2026-06-10T08:00:00+00:00",
      "2026-06-11T22:00:00+00:00"
    );
    const [startEl, endEl] = el.querySelectorAll("time");
    expect(startEl.textContent).toMatch(/2026/);
    expect(endEl.textContent).toMatch(/2026/);
  });

  it("includes a timezone indicator", () => {
    const el = makeElement(
      "2026-04-30T18:00:00+01:00",
      "2026-04-30T20:00:00+01:00"
    );
    const tz = el.querySelector(".dc-upcoming-events__tz");
    expect(tz).not.toBeNull();
    expect(tz?.textContent).toMatch(/^ \(.+\)$/);
  });

  it("leaves fallback content alone when attributes are missing", () => {
    const el = document.createElement("dc-event-time");
    el.innerHTML = "<span>fallback</span>";
    container.appendChild(el);
    expect(el.querySelector("span")?.textContent).toBe("fallback");
  });

  it("leaves fallback content alone when attributes are invalid dates", () => {
    const el = document.createElement("dc-event-time");
    el.setAttribute("start", "not-a-date");
    el.setAttribute("end", "also-not-a-date");
    el.innerHTML = "<span>fallback</span>";
    container.appendChild(el);
    expect(el.querySelector("span")?.textContent).toBe("fallback");
  });
});

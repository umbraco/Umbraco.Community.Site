import { describe, it, expect, vi, afterEach } from "vitest";
import "./dc-profile-building.element.js";

describe("dc-profile-building", () => {
  afterEach(() => {
    vi.useRealTimers();
    document.body.innerHTML = "";
  });

  it("reloads the page after the refresh interval", () => {
    vi.useFakeTimers();
    const reload = vi.fn();
    vi.stubGlobal("location", { ...window.location, reload });
    const el = document.createElement("dc-profile-building");
    el.dataset.refreshMs = "2000";
    document.body.appendChild(el);

    vi.advanceTimersByTime(1999);
    expect(reload).not.toHaveBeenCalled();
    vi.advanceTimersByTime(2);
    expect(reload).toHaveBeenCalledTimes(1);
    vi.unstubAllGlobals();
  });

  it("does not reload once removed from the page", () => {
    vi.useFakeTimers();
    const reload = vi.fn();
    vi.stubGlobal("location", { ...window.location, reload });
    const el = document.createElement("dc-profile-building");
    document.body.appendChild(el);
    el.remove();

    vi.advanceTimersByTime(10000);
    expect(reload).not.toHaveBeenCalled();
    vi.unstubAllGlobals();
  });
});

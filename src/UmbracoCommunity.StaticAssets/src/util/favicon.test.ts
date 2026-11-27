import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { FAVICON_RETRY_DELAYS, watchFavicon } from "./favicon";

function icon(src: string): HTMLImageElement {
  const img = document.createElement("img");
  img.loading = "lazy";
  watchFavicon(img);
  img.src = src;
  document.body.append(img);
  return img;
}

describe("favicon", () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
    document.body.innerHTML = "";
  });

  it("hides only the icon that failed", () => {
    const failed = icon("/proxy?token=a");
    const fine = icon("/proxy?token=b");

    failed.dispatchEvent(new Event("error"));

    expect(failed.hidden).toBe(true);
    expect(fine.hidden).toBe(false);
  });

  it("tries a failed icon again, eagerly, and shows it once it loads", () => {
    const img = icon("/proxy?token=a");
    const setSrc = vi.spyOn(img, "src", "set");

    img.dispatchEvent(new Event("error"));
    vi.advanceTimersByTime(FAVICON_RETRY_DELAYS[0] - 1);
    expect(setSrc).not.toHaveBeenCalled();

    vi.advanceTimersByTime(1);
    expect(setSrc).toHaveBeenCalledWith("/proxy?token=a");
    expect(img.loading).toBe("eager");

    img.dispatchEvent(new Event("load"));
    expect(img.hidden).toBe(false);
  });

  it("gives up after the last retry, leaving the icon hidden", () => {
    const img = icon("/proxy?token=a");
    const setSrc = vi.spyOn(img, "src", "set");

    for (const delay of FAVICON_RETRY_DELAYS) {
      img.dispatchEvent(new Event("error"));
      vi.advanceTimersByTime(delay);
    }
    img.dispatchEvent(new Event("error"));
    vi.advanceTimersByTime(60_000);

    expect(setSrc).toHaveBeenCalledTimes(FAVICON_RETRY_DELAYS.length);
    expect(img.hidden).toBe(true);
  });

  it("doesn't retry an icon that was removed or now shows another address", () => {
    const removed = icon("/proxy?token=a");
    const changed = icon("/proxy?token=b");
    const removedSrc = vi.spyOn(removed, "src", "set");

    removed.dispatchEvent(new Event("error"));
    changed.dispatchEvent(new Event("error"));
    removed.remove();
    changed.setAttribute("src", "/proxy?token=c");
    const changedSrc = vi.spyOn(changed, "src", "set");
    vi.advanceTimersByTime(FAVICON_RETRY_DELAYS[0]);

    expect(removedSrc).not.toHaveBeenCalled();
    expect(changedSrc).not.toHaveBeenCalled();
  });
});

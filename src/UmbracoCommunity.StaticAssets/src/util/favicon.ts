// A link's favicon comes through the site's image proxy, which can fail for a moment (a 429 from its rate limit, the
// remote site being slow). A failed icon is hidden on its own, so it never leaves a blank slot and never affects the
// other icons, and it is tried again a few times, so a failure that passes doesn't hide the icon for good.

/** How long to wait before each new try, in milliseconds. After the last one the icon stays hidden. */
export const FAVICON_RETRY_DELAYS = [2_000, 10_000, 30_000] as const;

const attempts = new WeakMap<HTMLImageElement, { src: string; count: number }>();

function retry(img: HTMLImageElement): void {
  img.hidden = true;
  const src = img.getAttribute("src");
  if (!src) return;

  const previous = attempts.get(img);
  const count = previous?.src === src ? previous.count : 0;
  if (count >= FAVICON_RETRY_DELAYS.length) return;
  attempts.set(img, { src, count: count + 1 });

  window.setTimeout(() => {
    // Gone, or already showing something else: nothing to retry.
    if (!img.isConnected || img.getAttribute("src") !== src) return;
    // A hidden lazy image is never loaded, so load this one straight away; `load` shows it again.
    img.loading = "eager";
    img.src = src;
  }, FAVICON_RETRY_DELAYS[count]);
}

/** For an icon's `load` event: shows it (again). */
export function onFaviconLoad(event: Event): void {
  const img = event.currentTarget as HTMLImageElement;
  img.hidden = false;
  attempts.delete(img);
}

/** For an icon's `error` event: hides only this icon and tries it again later. */
export function onFaviconError(event: Event): void {
  retry(event.currentTarget as HTMLImageElement);
}

/** Wires up an icon made outside a Lit template, including one that already failed before this ran. */
export function watchFavicon(img: HTMLImageElement): void {
  img.addEventListener("load", onFaviconLoad);
  img.addEventListener("error", onFaviconError);
  if (img.complete && img.naturalWidth === 0 && img.currentSrc) retry(img);
}

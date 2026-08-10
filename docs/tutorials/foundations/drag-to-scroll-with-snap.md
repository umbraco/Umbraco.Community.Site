---
tags: [web-components, slider, touch, lit]
---

# Building a touch-friendly drag-to-scroll slider in vanilla web components

Carousel libraries like Swiper and Embla are excellent, and most of what they give you is overkill for a block that shows a handful of cards or slides and needs prev/next navigation. This tutorial walks through `<dc-slider>` — a small custom element in this repo that handles touch drag (following the finger, snapping to the nearest slide on release), desktop hover-zone navigation, and an explicit-arrow-button opt-in, all without a dependency. It's a *foundation* piece, reused as-is by two different content blocks with zero component-side changes between them.

## Why you might want this

Once you strip away autoplay, infinite looping, and a plugin ecosystem, a slide carousel is a surprisingly small problem: track a horizontal offset, move it by one slide-width at a time, and let touch drag nudge that offset directly before snapping it to the nearest step. If that's genuinely all you need, writing it by hand keeps the bundle small and the behaviour fully legible — no framework option to look up, no library update to track, no bug filed upstream to wait on.

## What we're building

Two independent custom elements, coupled as loosely as two components can be:

1. **`<dc-slider>`** — a plain `HTMLElement`, not a `LitElement`. It owns touch-drag tracking, the snap-on-release calculation, and either hover-zone or explicit-arrow-button navigation depending on where it's used. The exact same element, with no configuration, is reused by both the Slider Block and the Blog Showcase Block.
2. **`<dc-slider-controls>`** — a separate `LitElement` progress indicator (a thin line with a moving pill and zero-padded position labels), which finds out what `<dc-slider>` is doing purely by listening for a `CustomEvent` on `document`. Neither component imports, extends, or otherwise knows about the other.

## Why the obvious fix doesn't work

**Reaching for a carousel library by default.** Swiper and Embla are genuinely good at what they do — but "good at what they do" includes touch physics, RTL support, a plugin system, and autoplay you don't need here. That capability has a bundle-size cost you pay on every page render, for features a fixed set of content blocks with known, simple requirements will never exercise.

**Native `overflow-x: auto` plus CSS `scroll-snap`, expecting it to give you drag-and-snap for free.** It's tempting, and for a lot of carousels it's the right call — but it can't express one thing this component specifically needs: telling the browser "wait, don't commit to a horizontal scroll gesture yet" while a touch is ambiguous between a horizontal swipe and a vertical page-scroll. A native scroll container captures the gesture immediately based on its own heuristics; there's no JS hook to defer that decision the way `getTouchMovePoint`'s direction check does (see Step 3). For a compact carousel embedded in a page someone is also trying to scroll vertically, that ambiguity matters.

**Auto-advancing on hover instead of just revealing a click target.** "Hover = navigate" sounds like a nice desktop affordance until you actually build it: resting your cursor anywhere near the edge of the carousel starts moving it without you asking it to, and the behaviour doesn't exist for touch or keyboard users at all. What this component actually does with "hover" is narrower and safer — see Step 5.

## Walkthrough

### Step 1 — A plain `HTMLElement`, not a `LitElement`

```ts
@customElement("dc-slider")
export class DcSlider extends HTMLElement {
```

Lit's `@customElement` decorator works on any class that extends `HTMLElement` — it doesn't require extending `LitElement`. `DcSlider` has no reactive state to render and no shadow DOM template, so there's nothing Lit's rendering model would buy here beyond the registration convenience the decorator already gives for free. Contrast this with `<dc-slider-controls>` (Step 7), which *does* extend `LitElement`, because it genuinely has UI — a progress track and pill — driven by state that changes over time.

### Step 2 — Work out the navigation mode once, on connect

```ts
connectedCallback() {
  const sliderBlock = this.closest(".dc-slider-block, .dc-blog-showcase-block");
  this.#hasExplicitButtons = sliderBlock?.classList.contains("has-buttons") ?? false;

  if (this.#hasExplicitButtons) {
    sliderBlock!.addEventListener("click", this.#arrowButtonHandler);
  } else {
    this.#createHoverZones();
  }
  this.#updateHoverZones();
}
```

`closest(".dc-slider-block, .dc-blog-showcase-block")` is the entire mechanism that makes one component work for two unrelated content blocks — it doesn't matter which ancestor class matches, only whether that ancestor also carries `has-buttons`. The Slider Block sets that class from an editor-facing "display buttons" toggle; the Blog Showcase Block sets it whenever its `PostDisplay` setting is `"Slider"` (there's no non-button slider mode for that block). Either way, `<dc-slider>` itself never has to know which block it's inside.

### Step 3 — Touch drag: decide direction first, then follow the finger

```ts
container.addEventListener("touchstart", this.getTouchStartPoint, { passive: true });
container.addEventListener("touchmove", this.getTouchMovePoint, { passive: false });
container.addEventListener("touchend", this.getTouchEndPoint, { passive: true });
```

Only real `TouchEvent`s here — no Pointer Events, no mouse handlers. (Desktop gets hover zones and buttons instead of a drag gesture; mouse-drag is a different component's problem, not this one's.) The [`passive` option](https://developer.mozilla.org/en-US/docs/Web/API/EventTarget/addEventListener) matters: `touchstart`/`touchend` are `passive: true` because they never call `preventDefault()`, which lets the browser start handling the touch optimistically without waiting on this listener. `touchmove` has to be `passive: false`, because it sometimes *does* call `preventDefault()` — and a passive listener that tries to would simply be ignored.

```ts
getTouchMovePoint = (event: TouchEvent) => {
  const currentX = event.changedTouches[0].clientX;
  const currentY = event.changedTouches[0].clientY;
  const deltaX = currentX - this.#touchstartX;
  const deltaY = currentY - this.#touchstartY;

  if (this.#isDragging) {
    event.preventDefault();
    const container = this.#getContainer();
    if (container) container.style.transform = `translateX(${this.#dragStartLeft + deltaX}px)`;
    return;
  }

  if (Math.abs(deltaX) > this.#swipeThreshold) {
    if (Math.abs(deltaX) > Math.abs(deltaY)) {
      this.#isDragging = true;
      event.preventDefault();
    }
  }
};
```

Nothing commits to dragging until the touch has moved more than a small threshold (`#swipeThreshold = 10`) *and* moved further horizontally than vertically. Below that threshold, or if the vertical component wins, this listener does nothing and the browser is free to scroll the page normally. Only once horizontal intent is confirmed does it call `preventDefault()` and start moving the slide strip via `transform: translateX(...)` — literally following the finger, one pixel at a time, by adding the raw drag delta to wherever the strip already was (`#dragStartLeft`).

### Step 4 — Snap-on-release is arithmetic, not CSS

There's no `scroll-snap-type`/`scroll-snap-align` anywhere in this codebase — the "snap" is entirely computed:

```ts
getTouchEndPoint = (event: TouchEvent) => {
  const container = this.#getContainer();
  if (!container) return;
  container.style.transition = "transform 0.3s ease";
  if (!this.#isDragging) return;

  const endX = event.changedTouches[0].clientX;
  const dragDelta = endX - this.#touchstartX;
  const { scrollStep } = this.#getContainerWithStep();
  const slidesOffset = Math.round(Math.abs(dragDelta) / scrollStep);

  if (slidesOffset > 0) {
    const maxIndex = this.#getMaxIndex();
    this.#currentIndex = dragDelta < 0
      ? Math.min(this.#currentIndex + slidesOffset, maxIndex)
      : Math.max(this.#currentIndex - slidesOffset, 0);
    this.#dispatchIndexChangedEvent();
  }

  this.#setTransform(container, this.#currentIndex * scrollStep * -1);
};
```

`scrollStep` is one slide's `offsetWidth` plus the container's `column-gap`, read live from the DOM (`#getContainerWithStep`) — no fixed slide-width constant to keep in sync with CSS. `slidesOffset` rounds the raw drag distance to the nearest whole number of slide-widths, so a fast, short flick and a slow, long drag that cover the same fraction of a slide land on the same result. `#currentIndex` is clamped to `[0, maxIndex]`, and re-enabling `transition: transform 0.3s ease` right before setting the final `transform` is what turns "jump straight there" into a smooth 300ms slide — the same transition property that gets set to `none` in `getTouchStartPoint` so the *drag itself* doesn't lag behind the finger waiting on a CSS transition to catch up.

### Step 5 — Hover zones are real buttons that fade in, not auto-scroll triggers

```ts
#buildZone(direction: "prev" | "next"): HTMLElement {
  const zone = document.createElement("button");
  zone.type = "button";
  zone.className = `slider-hover-zone slider-hover-zone--${direction}`;
  zone.setAttribute("aria-label", direction === "prev" ? "Previous slide" : "Next slide");
  zone.addEventListener("click", () => {
    this.#scrollContainer(direction);
    this.#dispatchIndexChangedEvent();
  });
  return zone;
}
```

A "hover zone" is a genuine `<button>`, injected into `.slides-wrapper` and absolutely positioned over the left/right edge by CSS. Hovering it does exactly one thing — fades an arrow overlay in via `opacity` — and nothing navigates until it's actually clicked:

```css
.slider-hover-zone {
    opacity: 0;
    transition: opacity 0.25s ease;
}
.slider-hover-zone:hover,
.slider-hover-zone:focus-visible {
    opacity: 1;
}
```

Being a real `<button>` with `:focus-visible` styling means it's independently reachable and operable by keyboard even though it's a JS-injected, hover-revealed element — Tab lands on it, focus makes the arrow visible the same way hover does, and Enter/Space activates it, all without this component writing a single line of key-handling code. These zones only exist on desktop-sized viewports (`.slider-hover-zone { display: none; }` below the `--md` breakpoint) — touch devices get the drag gesture from Step 3 instead.

### Step 6 — Delegating clicks to the ancestor, because the buttons live in two different places

When `has-buttons` is set, `<dc-slider>` doesn't render or own any buttons itself — it listens for clicks on its *ancestor* block:

```ts
#arrowButtonHandler = (event: Event) => {
  const button = (event.target as HTMLElement).closest("[data-slider-action]") as HTMLElement;
  if (!button) return;
  const action = button.dataset.sliderAction as "prev" | "next";
  this.#scrollContainer(action);
  this.#dispatchIndexChangedEvent();
};
```

That's necessary because the Razor markup renders two copies of the arrow buttons at different breakpoints — a desktop pair inside `.dc-slider-block__intro` (a sibling of `<dc-slider>`, not a descendant) and a mobile pair inside `.dc-slider-block__arrows--mobile` (which *is* inside `<dc-slider>`). Attaching one delegated listener to the shared `.dc-slider-block`/`.dc-blog-showcase-block` ancestor covers both, regardless of which pair happens to be visible at the current viewport width. `#updateHoverZones()` also disables whichever buttons are at the start/end of the slide range — `[data-slider-action='prev']` at index 0, `[data-slider-action='next']` at the last index — using the same `disabled` attribute the browser already knows how to style and ignore clicks on.

### Step 7 — A progress indicator that only ever listens

```ts
export class DcSliderControls extends LitElement {
  @state() currentIndex = 0;
  @property({ type: Number }) count = 0;

  firstUpdated() {
    document.addEventListener("dc-slider-index-changed", this.indexChanged);
  }

  indexChanged = (event: Event) => {
    const index = (event as CustomEvent)?.detail?.index ?? -1;
    if (index === -1) return;
    this.currentIndex = index;
  };
}
```

`<dc-slider>` dispatches `dc-slider-index-changed` (`bubbles: true, composed: true`) every time `#currentIndex` changes; `<dc-slider-controls>` listens for it on `document`, not on any specific element — no shared parent reference, no property binding, no shared base class. `count` arrives once, as a plain server-rendered attribute (`<dc-slider-controls count="@slideItems.Count">`) — it's the only piece of information that isn't event-driven. One quirk worth knowing if you're reading the render output literally: the left-hand label is `${this.#pad(1)}`, always `"01"` regardless of `currentIndex` — only the pill's position along the track moves; the numbers bracketing it are a fixed range label ("01" to the total count), not a live "you are here" counter. Hiding this component entirely in `has-buttons` mode is done by the *parent* CSS (`.dc-slider-block.has-buttons .mobile-controls { display: none; }`), not by any `has-buttons`-awareness inside the component — it has none.

## Alternatives we considered

- **Swiper or Embla.** The bundle-size trade-off cuts the other way if your carousel needs their feature set — multi-axis gestures, virtual slides, a plugin ecosystem. For a fixed, small set of content blocks with known requirements, that's capability you'd be paying for and never using.
- **Native `scrollLeft` dragging plus CSS `scroll-snap`.** This is genuinely how the *other* slider in this codebase, `<dc-image-slider>` (a continuous, optionally auto-scrolling hero gallery), works — and it's a reasonable choice there because that component doesn't have the same page-scroll-ambiguity concern a compact, page-embedded carousel does. Different component, different constraints, different answer — not a rejected alternative, just a different job.
- **A shared base class or mixin between `<dc-slider>` and `<dc-slider-controls>`.** Not built. The document-level `CustomEvent` is looser coupling than a shared class hierarchy would be — it costs a global event name instead of an import, but it means the progress indicator is genuinely optional: drop it from the markup and nothing about `<dc-slider>` needs to change.

## Trade-offs and known limits

- **No keyboard-arrow navigation on `<dc-slider>` itself.** The real `<button>`s (hover zones, arrow buttons) get native Tab/Enter/Space handling for free, but there's no `ArrowLeft`/`ArrowRight`-to-advance-slide behaviour anywhere in the component.
- **No live-region announcement of slide changes.** A screen-reader user who clicks "next" gets whatever their reader happens to notice from the DOM change itself — there's no explicit `aria-live` region announcing "slide 3 of 6."
- **The snap arithmetic has no dedicated unit test.** The colocated test suite checks event wiring and `preventDefault()` behaviour (does a horizontal drag get captured, does a tap-with-no-movement correctly not fire a slide change), not the `slidesOffset` calculation's actual output for a given drag distance.
- **Hover-zone opacity needs `:hover`.** Touchscreen laptops and other hybrid devices that never trigger a true hover state won't see the fade-in affordance — though they also don't need it, since they get the touch-drag gesture instead.

## Where to go next

- **[Frontend primer](../../primers/frontend.md#lit--postcss)** — the wider Lit conventions this component follows, and how its colocated `.test.ts` fits into the frontend's testing setup.
- **[`ACCESSIBILITY.md`](../../../ACCESSIBILITY.md)** — this site's WCAG conformance notes; the keyboard/live-region gaps above are worth reading in that light.
- The `<dc-image-slider>` component (the sibling, native-scroll-based slider mentioned in Alternatives) doesn't have its own tutorial in this suite yet — a natural companion piece if you're extending this one.

Hopefully that's enough to build — or debug — a carousel that doesn't need a dependency to feel right. Welcome aboard!

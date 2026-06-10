---
tags: [progressive-enhancement, mutation-observer, forms, web-components]
---

# Progressive enhancement of async-rendered DOM with MutationObserver

Progressive enhancement has a tidy mental model: the server sends working HTML, and your JavaScript layers extra behaviour on top once it loads. That model quietly assumes the HTML you want to enhance is *there* when your code runs. But what happens when it isn't yet — when the very thing you mean to enhance is rendered, asynchronously, by some other component that owns its own timing? Your enhancement runs, finds nothing to do, and gives up. This tutorial walks through the small `MutationObserver` pattern the Umbraco Community site uses to solve exactly that: the `<dc-form-steps>` element waits for an Umbraco Forms form to finish rendering, turns it into a multi-step form once it appears, and then disconnects cleanly.

It's a *foundation* piece, and although the worked example is Umbraco Forms, nothing about the pattern is. Any time you're enhancing DOM that a third-party widget, a framework island, or a lazily-hydrated component renders on its own schedule, this is the shape of the answer.

(If web components are new to you: a *custom element* is a class you register against a tag name, and the browser calls its `connectedCallback` when an instance is added to the page. MDN's [Using custom elements](https://developer.mozilla.org/en-US/docs/Web/API/Web_components/Using_custom_elements) is the primer; we lean on just `connectedCallback` and `disconnectedCallback` here.)

## Why you might want this

Picture the markup the server sends for a stepped form:

```cshtml
<dc-form-steps>
    <umb-forms-render form-id="@Model.Content.Form.Value" />
</dc-form-steps>
```

`<umb-forms-render>` is Umbraco Forms' own web component. When the page loads, *it* fetches the form definition and renders the fields — the `<input>`s, the `.umbraco-forms-fieldset` groups, the submit button — into itself, a tick or several later. Crucially, that happens **after** your `<dc-form-steps>` element's `connectedCallback` has already fired. So the naive version:

```js
connectedCallback() {
  const fieldsets = this.querySelectorAll(".umbraco-forms-fieldset");
  this.buildSteps(fieldsets); // fieldsets.length === 0 — the form isn't here yet
}
```

…finds nothing, builds nothing, and you ship a single-page form wondering why the steps never appeared. The enhancement ran *before the thing it enhances existed.*

This isn't unique to Umbraco Forms. Swap in any widget that renders on its own clock — a payment-provider iframe's fields, a CMS block that hydrates client-side, a third-party comments embed — and you have the same race. You don't control when their DOM lands, so you can't assume it's there at any particular moment.

## What we're building

A custom element, [`<dc-form-steps>`](../../../src/UmbracoCommunity.StaticAssets/src/components/form/form-steps.element.ts), that:

1. **Tries to enhance immediately** — in case the form is somehow already there (a fast path that avoids setting up an observer at all).
2. **If it isn't, watches for it** with a `MutationObserver`, re-trying the enhancement each time the subtree changes.
3. **Runs the enhancement exactly once**, then disconnects the observer so it isn't paying to watch a tree it's done with.
4. **Cleans up** if the host element itself is ever removed from the page.

One detail to flag before the walkthrough, because it surprises people: this element extends plain `HTMLElement`, **not** `LitElement`, even though the rest of the site's components are Lit.

```ts
import { customElement } from "lit/decorators.js";

@customElement("dc-form-steps")
export class FormStepsElement extends HTMLElement {
  // …
}
```

We borrow Lit's `@customElement` decorator purely for the registration sugar (it just calls `customElements.define` for us — it works on any `HTMLElement` subclass, Lit or not). But there's no Lit template here, because **we're not rendering our own DOM — we're enhancing DOM somebody else rendered.** A Lit `render()` method would want to own and re-render the element's contents, which is exactly the wrong instinct when those contents belong to `<umb-forms-render>`. Plain DOM manipulation is the honest tool for the job.

## Walkthrough

The whole component is one file. We'll follow the lifecycle: connect → observe → enhance → disconnect.

### Step 1 — Try first, observe second

`connectedCallback` runs the moment the browser attaches the element. We attempt the enhancement straight away — and only if that fails do we set up the observer:

```ts
connectedCallback() {
  if (this.tryInit()) return;

  // umb-forms-render loads the form asynchronously — watch for it to appear.
  this.observer = new MutationObserver(() => this.tryInit());
  this.observer.observe(this, { childList: true, subtree: true });
}
```

The `if (this.tryInit()) return;` is worth dwelling on. Most of the time the form genuinely *isn't* ready and `tryInit()` returns `false`, so we fall through to the observer. But if it ever *is* ready — a cached render, a synchronous code path, a future version of Umbraco Forms that renders eagerly — we enhance immediately and never construct an observer at all. Cheap insurance, and it means the observer is strictly the fallback, not the main path.

`observe(this, { childList: true, subtree: true })` is the key call. We watch `this` (the host element), and:

- **`childList: true`** — fire when children are added or removed.
- **`subtree: true`** — and not just direct children; anywhere in the descendant tree.

`subtree` matters because `<umb-forms-render>` doesn't append the fieldsets as direct children of `<dc-form-steps>` — they land deeper, inside the form structure it builds. Without `subtree`, we'd only hear about `<umb-forms-render>` itself appearing, not the fields populating inside it.

### Step 2 — The idempotent `tryInit`

Every observer callback calls the same `tryInit()`. It has to be safe to call repeatedly — the observer may fire many times as the form renders in chunks — and do its real work exactly once:

```ts
private tryInit(): boolean {
  if (this.initialized) return true;

  const root = this.getFormRoot();
  this.fieldsets = Array.from(
    root.querySelectorAll<HTMLElement>(FIELDSET_SELECTOR)
  );

  if (this.fieldsets.length <= 1) return false;

  this.initialized = true;
  this.observer?.disconnect();
  this.buildNav();
  this.goToStep(0);
  return true;
}
```

Three guards, each pulling its weight:

- **`if (this.initialized) return true;`** — the latch. Once we've enhanced, every later observer callback returns immediately. This is what makes "fire many times, act once" true.
- **`if (this.fieldsets.length <= 1) return false;`** — the readiness check, and it's quietly doing double duty. `0` fieldsets means *the form hasn't rendered yet* — keep waiting. But `1` fieldset means *the form rendered and it's a single group* — there's nothing to step through, so stepping would be pointless. The same `<= 1` test handles both "not ready" and "ready but not worth enhancing", and in both cases the right move is the same: don't build steps. (The cost is that these two cases are indistinguishable — see the trade-offs.)
- **`this.observer?.disconnect();`** — the instant we succeed, we stop watching. We got what we were waiting for; continuing to observe would be pure overhead on every future DOM change inside the form (and forms mutate plenty as users type and validation toggles).

### Step 3 — The shadow-DOM wrinkle

`<umb-forms-render>` *might* render into a shadow root rather than into the light DOM, depending on version and configuration. A `querySelectorAll` on `this` won't reach inside a shadow root, so we check for one first:

```ts
private getFormRoot(): ParentNode {
  // umb-forms-render may use shadow DOM — check for it
  const formRender = this.querySelector("umb-forms-render");
  return formRender?.shadowRoot ?? this;
}
```

If `<umb-forms-render>` exposes an open `shadowRoot`, we query *that*; otherwise we query the host element itself. Every later lookup — fieldsets, the navigation bar, the submit container — goes through `getFormRoot()` so it works identically in both cases. This is the kind of defensive shrug you end up making against a component whose internals you don't own: handle both shapes, move on.

(Note this only works if the shadow root is *open*. A closed shadow root returns `null` from `.shadowRoot` and there's no reaching into it — at which point light-DOM enhancement isn't possible at all. Umbraco Forms uses open shadow roots where it uses them, so we're fine.)

### Step 4 — Disconnecting cleanly (both ways)

There are two distinct moments where the observer needs to stop, and the component handles each:

1. **On success**, inside `tryInit` — `this.observer?.disconnect()`, covered above. The job's done; stop watching.
2. **On removal**, if the element leaves the page before the form ever rendered:

```ts
disconnectedCallback() {
  this.observer?.disconnect();
}
```

That second one is the easily-forgotten one. If a user navigates away (in a SPA-ish flow), or the block is removed, or the form simply never renders, an observer left running is a leak — it holds a reference to the element and keeps the callback alive. `disconnectedCallback` is the custom-element lifecycle's matching bookend to `connectedCallback`, and disconnecting there guarantees the observer can't outlive the element. The `?.` matters: we may never have created an observer (the Step 1 fast path), so we guard against disconnecting `null`.

### Step 5 — The enhancement itself

Once `tryInit` succeeds, the actual stepping is ordinary DOM work — included here briefly so the pattern feels complete, but it's the *least* interesting part. `buildNav` creates Previous/Next buttons and a "Step 2 of 4" indicator and injects them next to the form's own navigation; `goToStep` shows one fieldset and hides the rest:

```ts
private goToStep(index: number) {
  if (index < 0 || index >= this.fieldsets.length) return;
  this.currentStep = index;

  this.fieldsets.forEach((fieldset, i) => {
    fieldset.classList.toggle("dc-form-steps__step--active", i === index);
    fieldset.hidden = i !== index;
  });

  this.prevBtn.hidden = index === 0;
  this.nextBtn.hidden = index === this.fieldsets.length - 1;

  // The form's own submit/navigation only shows on the last step.
  const submitNav = this.getFormRoot().querySelector<HTMLElement>(".umbraco-forms-navigation");
  if (submitNav) submitNav.hidden = index !== this.fieldsets.length - 1;

  this.stepIndicator.textContent = `Step ${index + 1} of ${this.fieldsets.length}`;
}
```

Advancing runs a per-step validation pass (`validateCurrentStep`) that checks the required fields *in the current fieldset only* before letting you move on — text, checkbox, radio-group, and `data-val-required` are all handled, and the first invalid field is scrolled into view and focused. It's deliberately a light reimplementation of what Umbraco Forms validates on submit, so that step-to-step navigation gives immediate feedback rather than letting you fill three steps and discover a step-one error at the end.

The visibility itself is a collaboration with CSS — the JS toggles a class and the `hidden` attribute, and [`form-steps.css`](../../../src/UmbracoCommunity.StaticAssets/src/css/form/form-steps.css) does the showing and hiding:

```css
dc-form-steps .umbraco-forms-fieldset { display: none; }
dc-form-steps .umbraco-forms-fieldset.dc-form-steps__step--active { display: block; }
```

### Step 6 — Wiring it from Razor

The element is registered by importing it (it self-registers via the `@customElement` decorator), and the public-site bundle pulls it in through [`src/components/index.ts`](../../../src/UmbracoCommunity.StaticAssets/src/components/index.ts). The view opts in per form, driven by an editor toggle:

```cshtml
@if (isStepped)
{
    <dc-form-steps>
        <umb-forms-render form-id="@Model.Content.Form.Value" />
    </dc-form-steps>
}
else
{
    <umb-forms-render form-id="@Model.Content.Form.Value" />
}
```

This is the progressive-enhancement contract kept honest: when stepping is off — *or* if the JavaScript fails to load, *or* if the bundle 404s — you still get a perfectly working `<umb-forms-render>` form. The custom element is an upgrade applied to working HTML, never a prerequisite for it. An unrecognised `<dc-form-steps>` tag is just an inert wrapper the browser renders as a block; the form inside works regardless.

## Alternatives we considered

- **Polling with `setInterval`/`setTimeout`.** "Check every 100ms until the fieldsets show up." It works, but you're trading correctness for guesswork: poll too fast and you burn cycles, too slow and the form visibly flashes un-stepped before snapping into place. A `MutationObserver` fires *exactly* when the DOM changes — no interval to tune, no wasted wake-ups, no flash. This is the headline reason to reach for it.
- **A fixed `setTimeout` delay.** "Wait 500ms, then enhance." Fragile in both directions: too short on a slow connection and the form still isn't there; needlessly laggy on a fast one. And it'll eventually break the day Umbraco Forms changes its render timing. Guessing at someone else's async timing is a bug waiting to happen.
- **The `load` event.** `window.onload` fires after *all* resources finish — but `<umb-forms-render>` fetches and renders on its own schedule, which may be after `load`. It's neither reliable nor tight enough.
- **Hooking `<umb-forms-render>`'s own lifecycle.** If the component dispatched a "rendered" event, we'd listen for that and skip the observer entirely — it'd be the cleanest option. It doesn't (not a public one we can rely on), and reaching into its internals to await some private promise would couple us to implementation details that can change under us. The MutationObserver treats the third-party component as the black box it is: we don't care *how* or *when* it renders, only that the DOM changed and we should re-check.
- **A Lit component with reactive rendering.** Covered above — Lit wants to own the element's DOM, and here the DOM belongs to someone else. Wrong tool.

## Trade-offs and known limits

- **"Not ready" and "single group" are indistinguishable.** The `fieldsets.length <= 1` test can't tell a form that hasn't rendered yet (`0`) from a genuinely single-group form (`1`) from a still-rendering form that's emitted its first fieldset but not its second. For a single-group form, the observer simply keeps watching, finds nothing to do, and is eventually disconnected when the element is removed — harmless but not free. If you needed to distinguish them, you'd wait for a definite "done" signal (a known terminal element, a count that stabilises across two ticks) rather than a threshold.
- **No give-up timeout.** If the form never renders — a failed fetch inside `<umb-forms-render>`, a misconfigured form id — the observer watches forever (well, until the element is removed). It never errors or logs. For a richer version you might add a timeout that disconnects and reports after N seconds, so a silently-broken form is visible in logs rather than just un-stepped.
- **`subtree: true` observes everything underneath.** Watching the whole subtree means the callback fires for *every* descendant mutation while the form renders, and we re-run `tryInit` each time (the `initialized` latch makes the post-success ones cheap, but pre-success it re-queries the fieldsets on every batch). For a form that's fine. For a host with a large, busy subtree you'd scope the observer more tightly — observe a specific child, or drop `subtree` if the thing you're waiting for arrives as a direct child.
- **Re-implements validation.** `validateCurrentStep` duplicates a slice of Umbraco Forms' own required-field validation so each step can be gated before advancing. That's a deliberate copy, and it can drift from the server's rules — a custom validator Umbraco Forms knows about won't be enforced step-to-step, only on final submit. The server-side validation is still the source of truth; the per-step pass is a UX nicety, not a security boundary.
- **Open shadow root required.** If `<umb-forms-render>` ever rendered into a *closed* shadow root, `getFormRoot` couldn't reach the fieldsets and the enhancement would silently no-op. We rely on it being open (or light DOM). Worth knowing if a future version changes that.

## Where to go next

The same "do something cheaply and politely in response to the DOM, then stop" instinct shows up in its sibling browser observer:

→ [Intersection-observer-driven pausing of off-screen animation](intersection-observer-paused-animation.md) *(planned)* — `IntersectionObserver` instead of `MutationObserver`, but the same discipline: react to the platform telling you when something changed, rather than polling, and tear down cleanly.

For the wider picture of how components like this fit into the site's frontend, see the [frontend primer](../../primers/frontend.md).

Hopefully this takes the "but the thing I want to enhance isn't there yet" head-scratch off your plate — it's a small pattern, but it's the right one whenever you're layering behaviour onto DOM you didn't render.

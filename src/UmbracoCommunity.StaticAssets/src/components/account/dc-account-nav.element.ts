import { customElement } from "lit/decorators.js";

const elementName = "dc-account-nav";

/**
 * The account page's side menu. Server rendered, all panels visible and the menu links plain in-page anchors, so it all
 * works without JavaScript; once this element upgrades it shows one panel at a time:
 * - the address's hash picks the panel (`/account#timeline`), the first one when it names none; a hash naming something
 *   inside a panel opens that panel;
 * - choosing a menu link adds a history entry, so back and forward move between panels;
 * - the open panel's link gets `aria-current="page"`, and on a switch focus moves to the panel's heading.
 *
 * Markup: menu links carry `data-account-nav-link` and `href="#{panel id}"`; panels carry `data-account-panel` and an
 * id, and start with a heading (`tabindex="-1"`).
 */
@customElement(elementName)
export class AccountNavElement extends HTMLElement {
  #panels: HTMLElement[] = [];
  #links: HTMLAnchorElement[] = [];
  #active: HTMLElement | null = null;

  readonly #onClick = (event: MouseEvent) => {
    if (event.defaultPrevented || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) {
      return;
    }
    const link = (event.target as Element | null)?.closest<HTMLAnchorElement>("a[data-account-nav-link]");
    const panel = link ? this.#panelFor(link.hash) : null;
    if (!link || !panel) return;

    event.preventDefault();
    if (panel !== this.#active) {
      history.pushState(history.state, "", link.hash);
    }
    this.#show(panel, true);
  };

  // Back and forward between panels, and links elsewhere on the page that point at a panel.
  readonly #onLocationChange = () => {
    const panel = this.#panelFor(window.location.hash) ?? this.#panels[0];
    if (panel && panel !== this.#active) this.#show(panel, true);
  };

  connectedCallback() {
    this.#panels = [...this.querySelectorAll<HTMLElement>("[data-account-panel]")];
    this.#links = [...this.querySelectorAll<HTMLAnchorElement>("a[data-account-nav-link]")];
    if (this.#panels.length === 0) return;

    // On arrival the panel is just shown; focus stays where the browser put it.
    this.#show(this.#panelFor(window.location.hash) ?? this.#panels[0], false);

    this.addEventListener("click", this.#onClick);
    window.addEventListener("popstate", this.#onLocationChange);
    window.addEventListener("hashchange", this.#onLocationChange);
  }

  disconnectedCallback() {
    this.removeEventListener("click", this.#onClick);
    window.removeEventListener("popstate", this.#onLocationChange);
    window.removeEventListener("hashchange", this.#onLocationChange);
  }

  /** The panel a hash names: the panel itself, or the panel holding the element it names. */
  #panelFor(hash: string): HTMLElement | null {
    const id = decodeURIComponent(hash.replace(/^#/, ""));
    if (!id) return null;
    const target = this.ownerDocument.getElementById(id);
    if (!target || !this.contains(target)) return null;
    return this.#panels.find((panel) => panel === target || panel.contains(target)) ?? null;
  }

  #show(panel: HTMLElement, moveFocus: boolean) {
    this.#active = panel;
    for (const other of this.#panels) {
      other.hidden = other !== panel;
    }
    for (const link of this.#links) {
      if (link.hash === `#${panel.id}`) {
        link.setAttribute("aria-current", "page");
        this.#scrollLinkIntoRow(link);
      } else {
        link.removeAttribute("aria-current");
      }
    }

    if (moveFocus) {
      const heading = panel.querySelector<HTMLElement>("h2, h3, [tabindex='-1']");
      heading?.focus({ preventScroll: true });
      // Bring the panel's top into view, clear of the site's fixed header (the panel's scroll margin): on small
      // screens the menu sits above it, and a long panel may have been scrolled far down.
      const clearOfHeader = parseFloat(getComputedStyle(panel).scrollMarginTop) || 0;
      if (panel.getBoundingClientRect().top < clearOfHeader) panel.scrollIntoView?.({ block: "start" });
    }
  }

  /** In the menu's scrolling row on small screens, keeps the open panel's link in view (sideways only, never the page). */
  #scrollLinkIntoRow(link: HTMLElement) {
    const row = link.closest<HTMLElement>(".account-page__nav");
    if (!row || row.scrollWidth <= row.clientWidth) return;
    const rowRect = row.getBoundingClientRect();
    const linkRect = link.getBoundingClientRect();
    if (linkRect.left < rowRect.left) row.scrollLeft -= rowRect.left - linkRect.left;
    else if (linkRect.right > rowRect.right) row.scrollLeft += linkRect.right - rowRect.right;
  }
}

declare global {
  interface HTMLElementTagNameMap {
    [elementName]: AccountNavElement;
  }
}

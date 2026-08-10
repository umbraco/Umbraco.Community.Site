const elementName = "dc-nav-accordion";

// Below this width the sidebar goes full-width above the content (see
// documentation.css's --md breakpoint, 1024px) — that's the only case worth
// collapsing sections for; a narrow sticky sidebar already scrolls itself.
const narrowQuery = "(max-width: 1023px)";

/**
 * Deliberately *not* built on <details>/<summary>: collapsed <details> content is
 * hidden via a browser-internal mechanism (Chrome ties it to "Ctrl+F find in page"
 * jump-to-match support) that plain CSS `display`/`content-visibility` overrides
 * don't reliably undo — confirmed the hard way. A plain `hidden` attribute plus a
 * JS-driven toggle button has no such surprises, and CSS can't touch this element
 * without JS running, so if the script never runs (disabled, or blocked), every
 * section just stays in its original, fully-expanded, server-rendered state.
 *
 * Reacts to the viewport crossing the breakpoint live (not just at page load) via
 * matchMedia's change event — resizing/rotating without a reload collapses or
 * restores the sections rather than getting stuck in whatever state page load saw.
 */
class NavAccordionElement extends HTMLElement {
  #mediaQuery = window.matchMedia(narrowQuery);
  #boundOnChange = this.#onChange.bind(this);
  #collapsed = false;

  connectedCallback() {
    this.#mediaQuery.addEventListener("change", this.#boundOnChange);
    this.#applyState(this.#mediaQuery.matches);
  }

  disconnectedCallback() {
    this.#mediaQuery.removeEventListener("change", this.#boundOnChange);
  }

  #onChange(event: MediaQueryListEvent) {
    this.#applyState(event.matches);
  }

  #applyState(isNarrow: boolean) {
    if (isNarrow === this.#collapsed) return;
    this.#collapsed = isNarrow;
    if (isNarrow) this.#collapseSections();
    else this.#expandSections();
  }

  #collapseSections() {
    const sections = this.querySelectorAll<HTMLLIElement>(".documentation-nav__section");
    let nextId = 0;

    sections.forEach((section) => {
      const list = section.querySelector<HTMLUListElement>(":scope > .documentation-nav__list");
      const link = section.querySelector<HTMLAnchorElement>(":scope > .documentation-nav__section-link");
      if (!list || !link) return;

      const isActive = section.classList.contains("is-active");
      const id = list.id || `documentation-nav-section-${nextId++}`;
      list.id = id;
      if (!isActive) list.hidden = true;

      link.classList.add("documentation-nav__section-link--has-toggle");

      const toggle = document.createElement("button");
      toggle.type = "button";
      toggle.className = "documentation-nav__section-toggle";
      toggle.setAttribute("aria-expanded", isActive ? "true" : "false");
      toggle.setAttribute("aria-controls", id);
      toggle.setAttribute("aria-label", `Toggle ${link.textContent ?? "section"}`);
      toggle.innerHTML = '<span aria-hidden="true"></span>';
      toggle.addEventListener("click", () => {
        const nowHidden = list.toggleAttribute("hidden");
        toggle.setAttribute("aria-expanded", nowHidden ? "false" : "true");
      });

      link.insertAdjacentElement("afterend", toggle);
    });
  }

  #expandSections() {
    this.querySelectorAll<HTMLButtonElement>(".documentation-nav__section-toggle").forEach((toggle) => toggle.remove());
    this.querySelectorAll<HTMLAnchorElement>(".documentation-nav__section-link--has-toggle").forEach((link) =>
      link.classList.remove("documentation-nav__section-link--has-toggle")
    );
    this.querySelectorAll<HTMLUListElement>(".documentation-nav__list").forEach((list) => {
      list.hidden = false;
    });
  }
}

customElements.define(elementName, NavAccordionElement);

declare global {
  interface HTMLElementTagNameMap {
    [elementName]: NavAccordionElement;
  }
}

export { NavAccordionElement };

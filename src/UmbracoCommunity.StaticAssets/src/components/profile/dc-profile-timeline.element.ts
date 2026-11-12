import { customElement } from "lit/decorators.js";

const elementName = "dc-profile-timeline";

/** The query string parameter that selects the type filter. Keep in sync with ProfileTimeline.TypeQueryKey. */
export const TYPE_QUERY_KEY = "type";

/**
 * Filters the server-rendered profile timeline by type, in place. The server renders the filter as a
 * plain GET form and already applies `?type=` (so a shared link is filtered with or without JavaScript);
 * this element filters without reloading and keeps `?type=` in the address, leaving any other query
 * string values as they are. Entries carry `data-type`; a month with no matching entries is hidden, and
 * when nothing matches the `[data-timeline-empty]` message is shown. An unknown type shows everything.
 */
@customElement(elementName)
export class ProfileTimelineElement extends HTMLElement {
  #typeSelect: HTMLSelectElement | null = null;
  readonly #onChange = () => {
    this.apply();
    this.#updateUrl();
  };

  connectedCallback() {
    this.#typeSelect = this.querySelector<HTMLSelectElement>("[data-timeline-type]");
    if (!this.#typeSelect) {
      this.apply();
      return;
    }

    // The form's own button is only needed without JavaScript.
    this.querySelector<HTMLElement>("[data-timeline-apply]")?.setAttribute("hidden", "");
    this.querySelector<HTMLFormElement>("form[data-timeline-filters]")?.addEventListener("submit", this.#onSubmit);

    const requested = new URL(window.location.href).searchParams.get(TYPE_QUERY_KEY) ?? "";
    this.#typeSelect.value = this.#isOption(requested) ? requested : "";
    this.#typeSelect.addEventListener("change", this.#onChange);
    this.apply();
  }

  disconnectedCallback() {
    this.#typeSelect?.removeEventListener("change", this.#onChange);
    this.querySelector<HTMLFormElement>("form[data-timeline-filters]")?.removeEventListener("submit", this.#onSubmit);
  }

  readonly #onSubmit = (event: Event) => {
    event.preventDefault();
    this.#onChange();
  };

  #isOption(value: string): boolean {
    return [...(this.#typeSelect?.options ?? [])].some((option) => option.value === value);
  }

  /** Shows the entries that match the selected type (an empty value matches everything). */
  apply() {
    const type = this.#typeSelect?.value ?? "";
    let shown = 0;

    this.querySelectorAll<HTMLElement>("[data-timeline-month]").forEach((month) => {
      let shownInMonth = 0;
      month.querySelectorAll<HTMLElement>("[data-timeline-entry]").forEach((entry) => {
        const matches = type === "" || entry.dataset.type === type;
        entry.hidden = !matches;
        if (matches) shownInMonth++;
      });
      month.hidden = shownInMonth === 0;
      shown += shownInMonth;
    });

    const empty = this.querySelector<HTMLElement>("[data-timeline-empty]");
    if (empty) {
      empty.hidden = shown > 0;
    }
  }

  /** Puts the selected type in the address (or takes it out for everything), keeping other query values. */
  #updateUrl() {
    const url = new URL(window.location.href);
    const type = this.#typeSelect?.value ?? "";
    if (type) {
      url.searchParams.set(TYPE_QUERY_KEY, type);
    } else {
      url.searchParams.delete(TYPE_QUERY_KEY);
    }
    window.history.replaceState(window.history.state, "", url);
  }
}

declare global {
  interface HTMLElementTagNameMap {
    [elementName]: ProfileTimelineElement;
  }
}

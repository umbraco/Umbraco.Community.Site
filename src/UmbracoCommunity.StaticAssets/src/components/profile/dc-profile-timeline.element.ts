import { customElement } from "lit/decorators.js";

const elementName = "dc-profile-timeline";

/**
 * Enhances the server-rendered profile timeline. Filtering and paging are done by the server (a plain GET form with
 * `?type=`, and page links with `?page=`), so both work without JavaScript; this element only makes them nicer:
 * - choosing a type submits the filter form straight away, so its own button is hidden;
 * - a card image whose shape is far from 16:9 (a portrait or square photo) is marked `is-contained`, so the CSS shows it
 *   whole instead of cropping most of it away.
 */
@customElement(elementName)
export class ProfileTimelineElement extends HTMLElement {
  #typeSelect: HTMLSelectElement | null = null;

  readonly #onChange = () => {
    const form = this.#typeSelect?.form;
    if (form) form.requestSubmit();
  };

  connectedCallback() {
    this.#markOddShapedImages();

    this.#typeSelect = this.querySelector<HTMLSelectElement>("[data-timeline-type]");
    if (!this.#typeSelect) return;

    // The form's own button is only needed without JavaScript.
    this.querySelector<HTMLElement>("[data-timeline-apply]")?.setAttribute("hidden", "");
    this.#typeSelect.addEventListener("change", this.#onChange);
  }

  disconnectedCallback() {
    this.#typeSelect?.removeEventListener("change", this.#onChange);
  }

  #markOddShapedImages() {
    this.querySelectorAll<HTMLImageElement>(".community-profile__timeline-card-picture").forEach((img) => {
      const mark = () => {
        if (!img.naturalWidth || !img.naturalHeight) return;
        const ratio = img.naturalWidth / img.naturalHeight;
        img.parentElement?.classList.toggle("is-contained", ratio < 1.4 || ratio > 2.4);
      };
      if (img.complete) mark();
      else img.addEventListener("load", mark, { once: true });
    });
  }
}

declare global {
  interface HTMLElementTagNameMap {
    [elementName]: ProfileTimelineElement;
  }
}

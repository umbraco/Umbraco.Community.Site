import { customElement } from "lit/decorators.js";
import { markOddShapedImage } from "./profile-timeline-entry.js";

const elementName = "dc-profile-timeline";

// Lucide "maximize-2" and "x"; static markup, set with innerHTML.
const svgAttributes =
  'xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"';
const enlargeIcon = `<svg ${svgAttributes}><path d="M15 3h6v6"/><path d="m21 3-7 7"/><path d="m3 21 7-7"/><path d="M9 21H3v-6"/></svg>`;
const closeIcon = `<svg ${svgAttributes}><path d="M18 6 6 18"/><path d="m6 6 12 12"/></svg>`;

/**
 * Shows a picture enlarged in a modal dialog: at its own size, scaled down only when the window is smaller. Closes with
 * its button, Escape or a click outside the picture, and gives focus back to `opener`. The dialog only exists while open.
 */
function openPicture(src: string, title: string, opener: HTMLElement) {
  const dialog = document.createElement("dialog");
  dialog.className = "dc-picture-dialog";
  dialog.setAttribute("aria-label", title ? `Picture: ${title}` : "Picture");

  const close = document.createElement("button");
  close.type = "button";
  close.className = "dc-picture-dialog__close";
  close.setAttribute("aria-label", "Close the picture");
  close.innerHTML = closeIcon;

  const image = document.createElement("img");
  image.className = "dc-picture-dialog__image";
  image.src = src;
  image.alt = title;

  dialog.append(close, image);
  document.body.append(dialog);

  // Keep the page from scrolling behind the dialog, without it jumping sideways as the scrollbar goes.
  const scrollbarWidth = window.innerWidth - document.documentElement.clientWidth;
  document.body.style.paddingRight = `${scrollbarWidth}px`;
  document.body.classList.add("dialog-open");

  close.addEventListener("click", () => dialog.close());
  // A click on the dialog itself is a click on the backdrop around the picture.
  dialog.addEventListener("click", (event) => {
    if (event.target === dialog) dialog.close();
  });
  dialog.addEventListener(
    "close",
    () => {
      dialog.remove();
      document.body.style.paddingRight = "";
      document.body.classList.remove("dialog-open");
      opener.focus();
    },
    { once: true }
  );

  dialog.showModal();
}

/**
 * Enhances the server-rendered profile timeline. Filtering and paging are done by the server (a plain GET form with
 * `?type=`, and page links with `?page=`), so both work without JavaScript; this element only makes them nicer:
 * - choosing a type submits the filter form straight away, so its own button is hidden;
 * - a card image whose shape is far from 16:9 (a portrait or square photo) is marked `is-contained`, so the CSS shows it
 *   whole instead of cropping most of it away;
 * - each card picture gets a button over it that opens the picture enlarged, at its own size (as far as the window
 *   allows), in a dialog. The card itself stays one link to the entry's page, which is all there is without JavaScript.
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
    this.#addEnlargeButtons();

    this.#typeSelect = this.querySelector<HTMLSelectElement>("[data-timeline-type]");
    if (!this.#typeSelect) return;

    // The form's own button is only needed without JavaScript.
    this.querySelector<HTMLElement>("[data-timeline-apply]")?.setAttribute("hidden", "");
    this.#typeSelect.addEventListener("change", this.#onChange);
  }

  disconnectedCallback() {
    this.#typeSelect?.removeEventListener("change", this.#onChange);
  }

  /**
   * Puts a button over each card's picture. It is a sibling of the card (a link can't hold a button), laid over the
   * picture by the CSS.
   */
  #addEnlargeButtons() {
    this.querySelectorAll<HTMLImageElement>(".community-profile__timeline-card-picture").forEach((picture) => {
      const card = picture.closest<HTMLElement>(".community-profile__timeline-card");
      const body = card?.parentElement;
      if (!card || !body || body.querySelector(".community-profile__timeline-enlarge")) return;

      const title = card.querySelector(".community-profile__timeline-title")?.textContent?.trim() ?? "";
      const button = document.createElement("button");
      button.type = "button";
      button.className = "community-profile__timeline-enlarge";
      button.title = "Show the picture enlarged";
      button.setAttribute("aria-label", title ? `Show the picture enlarged: ${title}` : "Show the picture enlarged");
      button.innerHTML = enlargeIcon;
      button.addEventListener("click", () => openPicture(picture.currentSrc || picture.src, title, button));
      card.after(button);
    });
  }

  #markOddShapedImages() {
    this.querySelectorAll<HTMLImageElement>(".community-profile__timeline-card-picture").forEach(markOddShapedImage);
  }
}

declare global {
  interface HTMLElementTagNameMap {
    [elementName]: ProfileTimelineElement;
  }
}

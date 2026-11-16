import { customElement } from "lit/decorators.js";

const elementName = "dc-profile-building";

/**
 * Shown on a profile whose timeline is still being built (the server is checking the member's social posts for copies of
 * each other, a batch per visit). Reloads the page every `data-refresh-ms` (default 2.5 seconds) until it is done: the
 * server then renders the timeline instead of this element, so the refreshing stops by itself. Without JavaScript the
 * page can be reloaded by hand.
 */
@customElement(elementName)
export class ProfileBuildingElement extends HTMLElement {
  #timer: number | undefined;

  connectedCallback() {
    const ms = Number(this.dataset.refreshMs) || 2500;
    this.#timer = window.setTimeout(() => window.location.reload(), Math.max(1000, ms));
  }

  disconnectedCallback() {
    window.clearTimeout(this.#timer);
  }
}

declare global {
  interface HTMLElementTagNameMap {
    [elementName]: ProfileBuildingElement;
  }
}

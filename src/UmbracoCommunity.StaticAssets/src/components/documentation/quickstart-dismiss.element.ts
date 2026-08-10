import { LitElement, css, html } from "lit";
import { customElement, property } from "lit/decorators.js";

const elementName = "dc-quickstart-dismiss";
const storageKey = "documentation-quickstart-dismissed";

@customElement(elementName)
export class QuickstartDismissElement extends LitElement {
  @property({ type: String })
  target = "";

  connectedCallback() {
    super.connectedCallback();
    if (this.#isDismissed()) this.#hideTarget();
  }

  #isDismissed(): boolean {
    try {
      return window.localStorage.getItem(storageKey) === "1";
    } catch {
      return false;
    }
  }

  #hideTarget() {
    if (!this.target) return;
    document.getElementById(this.target)?.setAttribute("hidden", "");
  }

  #onClick() {
    try {
      window.localStorage.setItem(storageKey, "1");
    } catch {
      // localStorage unavailable (private browsing, disabled storage) — hides for this
      // view only, since there's nothing durable to remember it by without it.
    }
    this.#hideTarget();
  }

  render() {
    return html`
      <button type="button" @click=${this.#onClick}>
        <svg viewBox="0 0 16 16" width="14" height="14" aria-hidden="true">
          <path d="M2 2L14 14M14 2L2 14" stroke="currentColor" stroke-width="2" stroke-linecap="round" />
        </svg>
        Hide quick start
      </button>
    `;
  }

  static styles = css`
    button {
      display: inline-flex;
      align-items: center;
      gap: 0.4rem;
      font: inherit;
      font-size: 0.85rem;
      font-weight: 600;
      color: var(--color-dark-grey, #707070);
      background-color: var(--color-white, #fff);
      border: 1px solid var(--color-grey, #d9d9d9);
      padding: 0.4rem 0.75rem;
      border-radius: 6px;
      cursor: pointer;
      white-space: nowrap;
    }

    button:hover {
      color: var(--color-dark, #1b264f);
      border-color: var(--color-dark-grey, #707070);
      background-color: var(--color-light, #f1f0ee);
    }

    svg {
      flex: 0 0 auto;
    }
  `;
}

declare global {
  interface HTMLElementTagNameMap {
    [elementName]: QuickstartDismissElement;
  }
}

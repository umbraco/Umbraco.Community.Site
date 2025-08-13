import { arrowLeft, arrowRight } from "@umbraco-community/svg";
import { LitElement, css, html } from "lit";
import { customElement, property, state } from "lit/decorators.js";

const elementName = "dc-slider-controls";

@customElement(elementName)
export class DcSliderControls extends LitElement {
  @state()
  currentIndex = 0;

  @property({ type: Number })
  count = 0;

  firstUpdated() {
    document.addEventListener("dc-slider-index-changed", this.indexChanged);
  }

  disconnectedCallback() {
    document.removeEventListener("dc-slider-index-changed", this.indexChanged);
  }

  indexChanged = (event: Event) => {
    const index = (event as CustomEvent)?.detail?.index ?? -1;
    if (index === -1) return;
    this.currentIndex = index;
  }

  #handlePrevClick() {
    this.#dispatch("prev");
    this.#setCurrentIndex(-1);
  }

  #handleNextClick() {
    this.#dispatch("next");
    this.#setCurrentIndex(1);
  }

  #dispatch(action: "next" | "prev") {
    this.dispatchEvent(
      new CustomEvent("dc-slider-change", {
        detail: { action },
        bubbles: true,
        composed: true,
      })
    );
  }

  #setCurrentIndex(i: number) {
    const newIndex = this.currentIndex + i;
    if (newIndex < 0 || newIndex > this.count - 1) {
      this.currentIndex = 0;
      return;
    }

    this.currentIndex = newIndex;
  }

  #handleIndexClick(index: number) {
    if (index === this.currentIndex) return;

    this.dispatchEvent(
      new CustomEvent("dc-slider-change", {
        detail: { action: "index", index },
        bubbles: true,
        composed: true,
      })
    );

    this.currentIndex = index;
  }

  render() {
    return html` <div class="flex">
        <button
          class="nav-button"
          type="button"
          aria-label="Previous slide arrow"
          ?disabled=${this.currentIndex === 0}
          @click=${this.#handlePrevClick}
        >
          ${arrowLeft}
        </button>
        <button
          class="nav-button"
          type="button"
          aria-label="Next slide arrow"
          @click=${this.#handleNextClick}
        >
          ${arrowRight}
        </button>
      </div>
      <div id="mobileControlDots">
        ${[...Array(this.count)].map(
          (_, index) =>
            html`<span
              class="dot ${this.currentIndex === index ? "active" : ""}"
              @click="${() => this.#handleIndexClick(index)}"
            ></span>`
        )}
      </div>`;
  }

  static styles = [
    css`
      :host,
      .flex {
        display: flex;
      }
      .nav-button {
        display: flex;
        justify-content: center;
        align-items: center;
        width: var(--unit-md);
        height: var(--unit-md);
        border: none;
        padding: 0;
        background-color: transparent;
        cursor: pointer;
      }

      .nav-button:hover {
        background: transparent;
      }

      .nav-button[disabled] {
        pointer-events: none;
        opacity: 0.5;
      }

      #mobileControlDots {
        display: flex;
        align-items: center;
        gap: 7px;
      }

      .dot:before {
        display: block;
        content: " ";
        height: 10px;
        width: 10px;
        border-radius: 6px;
        outline: 1px solid var(--color-black);
        cursor: pointer;
      }

      .dot.active:before {
        background-color: var(--color-black);
        outline: none;
        width: 12px;
        height: 12px;
      }

      @media (min-width: 767px) {
        #mobileControlDots {
          display: none;
        }
      }
    `,
  ];
}

declare global {
  interface HTMLElementTagNameMap {
    [elementName]: DcSliderControls;
  }
}

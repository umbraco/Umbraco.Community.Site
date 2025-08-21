import {modernArrow} from "@umbraco-community/svg";
import {LitElement, css, html} from "lit";
import {customElement, property, state} from "lit/decorators.js";

const elementName = "dc-carousel-controls";

@customElement(elementName)
export class DcCarouselControls extends LitElement {
  @state()
  currentIndex = 0;

  @property({type: Number})
  count = 0;

  firstUpdated() {
    document.addEventListener("dc-carousel-index-changed", this.indexChanged);
  }

  disconnectedCallback() {
    document.removeEventListener("dc-carousel-index-changed", this.indexChanged);
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
      new CustomEvent("dc-carousel-change", {
        detail: {action},
        bubbles: true,
        composed: true,
      })
    );
  }

  #setCurrentIndex(i: number) {
    const newIndex = this.currentIndex + i;

    if (newIndex < 0) {
      this.currentIndex = this.count - 1;
      return;
    }

    if (newIndex > this.count - 1) {
      this.currentIndex = 0;
      return;
    }

    this.currentIndex = newIndex;
  }

  render() {
    return html`
      <div class="controls-arrows">
        <button
          class="nav-button prev"
          type="button"
          aria-label="Previous slide arrow"
          @click=${this.#handlePrevClick}
        >
          ${modernArrow}
        </button>
        <button
          class="nav-button"
          type="button"
          aria-label="Next slide arrow"
          @click=${this.#handleNextClick}
        >
          ${modernArrow}
        </button>
      </div>
      <div class="controls-numeric">
        <span>01</span>
        <div class="dots">
          ${[...Array(this.count)].map(
            (_, index) =>
              html`<span
                class="dot ${this.currentIndex === index ? "active" : ""}"
              ></span>`
          )}
        </div>
        <span>0${this.count}</span>
      </div>`;
  }

  static styles = [
    css`
      :host,
      .flex {
        display: flex;
      }

      .controls-arrows {
        display: flex;
        gap: 1rem;
        --color-disabled: #b5bad6;
      }

      button {
        display: flex;
        align-items: center;
        justify-content: center;
        height: 44px;
        width: 44px;
        background-color: none;
        border: 2px var(--color-blue) solid;
        border-radius: 50%;
        padding: 0;
        transition: all 0.25s;
      }

      button.prev svg {
        transform: rotate(180deg);
      }

      button:not([disabled]):hover {
        transform: scale(1.05);
        cursor: pointer;
      }

      button[disabled] {
        border-color: var(--color-disabled);
      }

      button[disabled] path {
        stroke: var(--color-disabled);
      }

      .controls-arrows button {
        background-color: var(--color-white);
      }

      .controls-numeric {
        display: flex;
        gap: 1rem;
      }

      .controls-numeric > span {
        color: var(--color-blue);
        font-size: 15px;
        font-weight: 500;
        line-height: normal;
        text-transform: uppercase;
      }

      .dots {
        display: flex;
        align-items: center;
        width: 162px;
      }

      .dot {
        flex: 1;
      }

      .dot:before {
        display: block;
        content: " ";
        height: 2px;
        background: #b5bad6;
      }

      .dot.active:before {
        background-color: var(--color-blue);
      }
    `,
  ];
}

declare global {
  interface HTMLElementTagNameMap {
    [elementName]: DcCarouselControls;
  }
}

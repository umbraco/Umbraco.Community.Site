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
  };

  #pad(n: number) {
    return n.toString().padStart(2, "0");
  }

  get #pillPosition() {
    const fraction = this.count <= 1 ? 0 : this.currentIndex / (this.count - 1);
    return `calc(${fraction * 100}% - ${fraction} * var(--pill-width))`;
  }

  render() {
    return html`
      <span class="progress-label">${this.#pad(1)}</span>
      <div class="progress-track" role="progressbar"
        aria-valuenow=${this.currentIndex + 1}
        aria-valuemin="1"
        aria-valuemax=${this.count}>
        <div class="progress-pill" style="left: ${this.#pillPosition}"></div>
      </div>
      <span class="progress-label">${this.#pad(this.count)}</span>
    `;
  }

  static styles = [
    css`
      :host {
        display: flex;
        align-items: center;
        justify-content: center;
        width: 100%;
        gap: 1rem;
        color: var(--slider-controls-color, var(--color-blue, #1b264f));
      }

      .progress-label {
        font-size: var(--font-size-sm, 0.875rem);
        line-height: 1;
        flex-shrink: 0;
      }

      .progress-track {
        position: relative;
        flex: 0 1 12rem;
        height: 2px;
        border-radius: 1px;
        overflow: hidden;
      }

      .progress-track::before {
        content: "";
        position: absolute;
        inset: 0;
        background: currentColor;
        opacity: 0.25;
      }

      .progress-pill {
        --pill-width: 1.5rem;
        position: absolute;
        top: 50%;
        left: 0;
        width: var(--pill-width);
        height: 3px;
        background: currentColor;
        border-radius: 2px;
        transform: translateY(-50%);
        transition: left 0.3s ease;
      }
    `,
  ];
}

declare global {
  interface HTMLElementTagNameMap {
    [elementName]: DcSliderControls;
  }
}

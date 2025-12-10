import {LitElement, html, css} from 'lit';
import {customElement, property, queryAssignedElements, state} from 'lit/decorators.js';

const elementName = "dc-testimonial-logos";

@customElement(elementName)
export class DcTestimonialLogosElement extends LitElement {
  @property()
  headline?: string;

  @property()
  count?: number;

  @property({attribute: 'animation-time', type: Number})
  animationTime: number = 10;

  @queryAssignedElements({slot: 'items'})
  slotItems?: any;

  @state()
  elementsWidth: number = 0;

  firstUpdated() {
    this.elementsWidth = this.slotItems.map(n => n.width).reduceRight((acc, cur) => acc + cur, 0);
  }

  render() {
    return html`
      <style>
        :host {
          --logos-items-width: ${this.elementsWidth}px;
          --logos-items-count: ${this.count};
          --logos-animation-time: ${this.animationTime}s;
        }
      </style>
      <p>${this.headline}</p>
      <div class="logos-barrier">
        <div class="logos">
          <slot name="items"></slot>
        </div>
      </div>
    `;
  }

  static styles = [
    css`
      :host {
        display: flex;
        position: relative;
        flex-direction: column;
        align-items: center;

        --logos-items-gap: 2rem;
      }

      p {
        color: var(--color-blue);
        font-size: 16px;
        font-style: normal;
        font-weight: 400;
        line-height: 19px; /* 118.75% */
        letter-spacing: 1.28px;
        text-transform: uppercase;
      }

      .logos-barrier {
        margin-top: 2rem;
        height: 50px;
        width: 100%;
        overflow: hidden;
        position: relative;
      }

      .logos-barrier::before,
      .logos-barrier::after {
        content: " ";
        position: absolute;
        z-index: 9;
        width: 90px;
        height: 100%;
      }

      .logos-barrier::before {
        top: 0;
        left: 0;
        background: linear-gradient(to right, rgba(241, 240, 238, 1) 0%, rgba(241, 240, 238, 0) 100%);
      }

      .logos-barrier::after {
        top: 0;
        right: 0;
        background: linear-gradient(to left, rgba(241, 240, 238, 1) 0%, rgba(241, 240, 238, 0) 100%);
      }

      .logos {
        display: flex;
        animation: translateinfinite var(--logos-animation-time) linear infinite;
        gap: var(--logos-items-gap);
        height: 50px;
        align-items: center;
      }

      .logos:hover {
        animation-play-state: paused;
      }

      @media (min-width: 1024px) {
        :host {
          flex-direction: row;
        }

        p {
          margin: auto 1rem auto auto;
          width: auto;
        }

        .logos-barrier {
          margin: auto;
          flex: 1;
        }
      }

      @keyframes translateinfinite {
        100% {
          transform: translateX(calc(-1 * (var(--logos-items-width) / 3 + (var(--logos-items-count) * var(--logos-items-gap)))));
        }
      }
    `,
  ];
}

class LogoItem {
  url: string = '';
  altText: string = '';
}

declare global {
  interface HTMLElementTagNameMap {
    [elementName]: DcTestimonialLogosElement;
  }
}

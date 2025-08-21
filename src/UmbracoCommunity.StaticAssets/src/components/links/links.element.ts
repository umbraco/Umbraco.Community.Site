import { LitElement, css, html } from "lit";
import { customElement } from "lit/decorators.js";

const elementName = "dc-links";

@customElement(elementName)
export class LinksElement extends LitElement {
  connectedCallback(): void {
    super.connectedCallback();

    // appending styles here rather than in dc-links-item
    // so we only add one style node, not one per link
    const style = document.createElement("style");
    style.textContent = `
      dc-links-item {
        a {
          display: inline-flex;
          align-items: center;
          font-size: 70px;
          font-weight: 400;
          line-height: 85px;
          text-decoration: none;
          color: var(--color-pink);
          z-index: 1;
          position: relative;
          text-decoration: none;
        }
      
        span {
          text-decoration: none;
          transition: padding-left 0.2s ease-in;
        }
      
        a:hover span {
          text-decoration: underline;
          text-decoration-thickness: 3px;
          text-underline-offset: 6px;
          padding-left: 76px;
        }
      
        a:hover + div {
          opacity: 0.6;
        }
      
        div {
          display: inline-flex;
          border-radius: 351px;
          overflow: hidden;
          position: absolute;
          width: 700px;
          height: 700px;
          opacity: 0;
          transition: opacity 0.3s ease-in;
          background-repeat: no-repeat;
          background-size: cover;
          background-color: lightgray;
          background-position: 50% center;
        }
      
        @media only screen and (max-width: 768px) {
          a {
            font-size: 38px;
            line-height: 56px;
          }
      
          a:hover span {
            padding-left: 24px;
          }
        }
      }
        
      .dc-links__link {
        font-size: 18px;
        font-weight: 400;
        line-height: 26px;
        color: var(--color-pink);
        margin-left: 300px;
        z-index: 1;
      }

      @media only screen and (max-width: 1023px) {
        .dc-links__link {
          margin-left: 150px;
        }
      }

      @media only screen and (max-width: 768px) {
        .dc-links__link {
          margin-left: 24px;
        }
      }`;

    this.appendChild(style);
  }

  render() {
    return html` <div class="dc-links">
      <div class="dc-links__container">
        <slot name="header"></slot>
        <div class="dc-links__items">
          <slot></slot>
        </div>
        <slot name="link"></slot>
      </div>
    </div>`;
  }

  static styles = [
    css`
      :host {
        position: relative;
        display: block;
        background-color: var(--color-dark);
        overflow-y: clip;

        --link-items-margin: 0 0 48px 24px;

        @media only screen and (min-width: 767px) {
          --link-items-margin: 0 0 95px 150px;
        }

        @media only screen and (min-width: 1023px) {
          --link-items-margin: 0 0 95px 300px;
        }
      }

      :host::before {
        display: block;
        content: "";
        position: absolute;
        top: 0;
        bottom: 0;
        width: 100vw;
        left: 50%;
        transform: translateX(-50%);
        background-color: var(--color-dark);
      }

      .dc-links {
        position: relative;
        max-width: var(--max-width);
        width: 100%;
        box-sizing: border-box;
      }

      .dc-links__container {
        position: relative;
        display: flex;
        flex-direction: column;
        max-width: var(--max-width);
        padding: var(--unit-lg) 0;

        @media only screen and (min-width: 767px) {
          padding: 95px 0 135px;
        }
      }

      dc-title-teaser {
        max-width: 588px;
        z-index: 1;
      }

      .dc-links__items {
        margin: var(--link-items-margin);
      }
    `,
  ];
}

declare global {
  interface HTMLElementTagNameMap {
    [elementName]: LinksElement;
  }
}

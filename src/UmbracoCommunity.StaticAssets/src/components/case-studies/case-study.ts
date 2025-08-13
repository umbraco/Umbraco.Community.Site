import { css, html, LitElement } from "lit";
import { customElement, property } from "lit/decorators.js";
import { ifDefined } from "lit/directives/if-defined.js";
import { when } from "lit/directives/when.js";
import { FilterableElement } from "../filters/filterable-element.element.js";

const elementName = "dc-case-study";

@customElement(elementName)
export class CaseStudyElement extends FilterableElement {
  @property({ attribute: "link-text" })
  linkText?: string;

  @property()
  link?: string;

  @property({ type: Array })
  type?: Array<string> = [];

  @property({ type: Array })
  skill?: Array<string> = [];

  @property({ type: Array })
  sector?: Array<string> = [];

  @property()
  country?: string;

  @property()
  partner?: string;

  render() {
    return html`<a href=${ifDefined(this.link)}
      ><slot name="logo"> </slot>
      <slot name="thumbnail"></slot>

      <div id="content">
        <div id="description">
          <slot name="name"></slot>
          <slot name="teaser"></slot>
        </div>
        <div id="meta">
          <p>${this.linkText ?? "Read the case study"}</p>
          ${when(this.partner, () => html`<uui-tag>${this.partner}</uui-tag>`)}
        </div>
      </div></a
    >`;
  }

  static styles = css`
    :host {
      position: relative;
      background: var(--color-white);
      display: flex;
      flex-direction: column;
      flex: 0 1 33%;

      --img-transform: scale(1);
      --header-color: var(--color-dark);
      --padding: var(--unit);
    }

    :host(:hover) {
      --img-transform: scale(1.1);
      --header-color: var(--color-blue);
    }

    [name="logo"] {
      display: none;
    }

    :host([has-logo]) [name="logo"] {
      display: block;
      height: 60px;
      width: 60px;
      position: absolute;
      z-index: 1;
      background: var(--color-white);
      padding: 3px;
      right: 20px;
      top: 20px;
      border-radius: 50%;
    }

    [name="logo"]::slotted(img) {
      height: 100%;
      border-radius: 50%;
      box-shadow: var(--base-box-shadow);
      margin: 0 !important;
    }

    ::slotted(h3) {
      color: var(--header-color);
      margin: 0;
    }

    [name="teaser"] {
      display: block;
      margin: var(--unit) 0 0;
    }

    [name="thumbnail"] {
      display: block;
      overflow: hidden;
    }

    [name="thumbnail"]::slotted(img) {
      transform: var(--img-transform);
      transition: transform 0.2s;
      width: 100%;
    }

    #content {
      flex:1;
    }

    @media (min-width: 768px) {
      :host {
        --padding: var(--unit-md);
      }

      #content {
        display: flex;
        flex-direction: column;
        justify-content: space-between;
      }
    }

    #description {
      padding: var(--padding);
      overflow-wrap: break-word;
    }

    #meta {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 0 var(--padding) var(--padding);
      flex-wrap: wrap;
    }

    #meta p {
      color: var(--color-blue);
      font-weight: bold;
      margin: 0;
    }

    a {
      text-decoration: none;
      color: var(--color-dark);
      height: 100%;
      display:flex;
      flex-direction:column;
    }
  `;
}

declare global {
  interface HTMLElementTagNameMap {
    [elementName]: CaseStudyElement;
  }
}

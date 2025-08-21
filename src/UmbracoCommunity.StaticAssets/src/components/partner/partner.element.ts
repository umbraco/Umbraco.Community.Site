import { getPartnershipColor, PartnershipLevels } from "@umbraco-community/util";
import { css, html } from "lit";
import { customElement, property } from "lit/decorators.js";
import { ifDefined } from "lit/directives/if-defined.js";
import { when } from "lit/directives/when.js";
import { FilterableElement } from "../filters/filterable-element.element.js";

export interface PartnerCard {
  name?: string;
  color?: string;
  coordinates?: string;
  link?: string;
  level?: string;
  country?: string;
  logo?: string;
}

const elementName = "dc-partner";

@customElement(elementName)
export class PartnerElement extends FilterableElement implements PartnerCard {
  @property({ type: Array })
  skill?: Array<string> = [];

  @property({ type: Array })
  sector?: Array<string> = [];

  @property()
  country?: string;

  @property()
  link?: string;

  @property()
  level?: string;

  @property()
  coordinates?: string;

  @property()
  color?: string;

  #cardHeader() {
    return html`<div id="header">
      <slot name="name"></slot>
      ${when(
      this.country,
      () => html`<dc-badge color="default">${this.country}</dc-badge>`
    )}
      <dc-badge
        backgroundColor=${getPartnershipColor(this.level)}
        textColor="var(--color-white)"
        >${this.level}</dc-badge
      >
    </div>`;
  }

  #cardBody() {
    if (
      this.level !== PartnershipLevels.Gold &&
      this.level !== PartnershipLevels.Platinum
    )
      return;

    return html`<div id="body">
      <slot style="background:${this.color}" name="thumbnail"></slot>
      <p>${this.skill?.join(", ")}</p>
    </div>`;
  }

  render() {
    return this.level !== PartnershipLevels.Gold && this.level !== PartnershipLevels.Platinum
      ? html`<div id="card">${this.#cardHeader()}</div>`
      : html`<a id="card" href=${ifDefined(this.link)}>${this.#cardHeader()} ${this.#cardBody()}</a>`;
  }

  static styles = css`
    :host {
      position: relative;
      display: flex;
      flex-direction: column;
      flex: 0 1 33%;

      --img-transform: scale(0.9);
      --header-color: var(--color-dark);
      --padding: var(--unit);
    }

    [name="name"] {
      color: var(--header-color);
    }

    [name="thumbnail"] {
      border-radius: var(--border-radius-xl);
      margin-bottom: var(--unit);
      overflow: hidden;
      display: flex;
      justify-content: center;
    }

    [name="thumbnail"]:hover {
      --img-transform: scale(1);
    }

    ::slotted(img) {
      transform: var(--img-transform);
      transition: transform 0.2s;
      width: 100%;
      max-width:300px !important;
    }

    ::slotted(h3) {
      margin: 0 var(--unit-sm) 0 0;
      line-height:1.1;
    }

    #card {
      display: flex;
      align-items: stretch;
      flex-direction: column;
      background: var(--color-white);
      height: 100%;
      width: 100%;
      box-sizing: border-box;
      border: var(--base-border);
      border-radius: var(--border-radius-xl);
      transition: box-shadow 0.3s ease-in-out 0s, border-color 120ms ease 0s;
    }

    #card:hover {
      border-color: var(--color-blue);
      box-shadow: var(--box-shadow-blue);
    }

    a {
      text-decoration: none;
      color: currentColor;
    }

    #header {
      padding: var(--unit);
      display: flex;
      align-items: center;
    }

    #header dc-badge:first-of-type {
      margin-left: auto;
    }

    #header dc-badge {
      margin-left: var(--unit-xs);
    }

    #body {
      padding: var(--unit);
      display:flex;
      flex:1;
      flex-direction:column;
      justify-content:space-between;
      border-top: var(--base-border);
    }

    #body p {
      margin: 0;
    }
  `;
}

declare global {
  interface HTMLElementTagNameMap {
    [elementName]: PartnerElement;
  }
}

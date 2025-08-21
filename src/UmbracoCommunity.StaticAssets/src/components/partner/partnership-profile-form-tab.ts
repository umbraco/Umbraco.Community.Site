import { css, html, LitElement } from "lit";
import { customElement, property } from "lit/decorators.js";

const elementName = "dc-partnership-profile-form-tab";

@customElement(elementName)
export class DcPartnershipProfileFormTabElement extends LitElement {
  @property()
  name!: string;

  @property({ attribute: "is-active" })
  isActive: boolean = false;

  render() {
    return this.isActive ? html`<slot></slot>` : '';
  }

  static styles = css`
    
  `;
}

declare global {
  interface HTMLElementTagNameMap {
    [elementName]: DcPartnershipProfileFormTabElement;
  }
}

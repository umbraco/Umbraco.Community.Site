import { css, html, LitElement } from "lit";
import { customElement } from "lit/decorators.js";

const elementName = "dc-filter-grid";

@customElement(elementName)
export class DcFilterGridElement extends LitElement {
  render() {
    return html`<slot></slot>`;
  }

  static styles = css`
    :host {
      display: grid;
      grid-template-columns: var(--columns);
      gap: var(--unit-md);
    }
  `;
}

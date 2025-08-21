import { close } from "@umbraco-community/svg";
import { css, html, LitElement } from "lit";
import { property } from "lit/decorators.js";

export abstract class DcDialogBaseElement extends LitElement {
  @property()
  header?: string;

  protected close() {
    this.dispatchEvent(
      new CustomEvent("dialog-close", { bubbles: true, composed: true })
    );
  }

  renderClose() {
    return html`<button id="close" @click=${this.close}>${close}</button>`;
  }

  abstract renderBody();

  render() {
    return html`${this.renderClose()}
      <h2>${this.header}</h2>
      ${this.renderBody()}`;
  }

  static styles = [
    css`
      #close {
        --fill: var(--color-dark);
        border: none;
        background: transparent;
        cursor: pointer;
        position: absolute;
        top: var(--unit);
        right: var(--unit);
        padding: 6px;
      }

      .lead,
      h2 {
        text-align: center;
      }
    `,
  ];
}

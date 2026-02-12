import { close } from "@umbraco-community/svg";
import { css, html, LitElement, nothing } from "lit";
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
    return html`<button
      id="close"
      class="close-button"
      @click=${this.close}
      aria-label="Close dialog"
      type="button"
    >${close}</button>`;
  }

  abstract renderBody();

  render() {
    return html`${this.renderClose()}
      ${this.header ? html`<h2 id="dialog-title">${this.header}</h2>` : nothing}
      ${this.renderBody()}`;
  }

  static styles = [
    css`
      :host {
        display: block;
      }

      #close {
        --fill: var(--color-dark);
        border: none;
        background: var(--color-white, #fff);
        cursor: pointer;
        position: sticky;
        top: 0;
        float: right;
        z-index: 10;
        padding: 8px;
        border-radius: 50%;
        box-shadow: 0 2px 8px rgba(0, 0, 0, 0.15);
        transition: background-color 0.2s ease, box-shadow 0.2s ease;
      }

      #close:hover {
        background: var(--color-grey-light, #f5f5f5);
        box-shadow: 0 4px 12px rgba(0, 0, 0, 0.2);
      }

      #close:focus-visible {
        outline: 2px solid var(--color-blue, #283a97);
        outline-offset: 2px;
      }

      #close:focus:not(:focus-visible) {
        outline: none;
      }

      .lead,
      h2 {
        text-align: center;
      }
    `,
  ];
}

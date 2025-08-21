import { LitElement, css, html } from "lit";
import { styleMap } from "lit/directives/style-map.js";
import { customElement, property } from "lit/decorators.js";
import { getAccessibleTextColor } from "@umbraco-community/util";

const dcBadgeElementName = "dc-badge";

@customElement(dcBadgeElementName)
export class DcBadge extends LitElement {
  @property()
  backgroundColor = "var(--color-white)";

  @property()
  textColor = "var(--color-black)";

  render() {
    const styles = {
      background: this.backgroundColor,
      color: this.textColor ?? getAccessibleTextColor(this.backgroundColor),
    };

    return html`<div style=${styleMap(styles)}><slot></slot></div>`;
  }

  static styles = [
    css`
      div {
        border-radius: 17px;
        min-height: 34px;
        display: flex;
        align-items: center;
        padding: 4px 16px;
        font-size: 12px;
        line-height: 14px;
        box-sizing: border-box;
      }

      :host([small]) div {
        height: auto;
        padding: 4px 8px;
      }

      :host([center]) div {
        justify-content: center;
      }
    `,
  ];
}

declare global {
  interface HTMLElementTagNameMap {
    [dcBadgeElementName]: DcBadge;
  }
}

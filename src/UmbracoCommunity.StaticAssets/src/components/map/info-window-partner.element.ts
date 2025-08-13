import { getPartnershipColor } from "@umbraco-community/util";
import { LitElement, css, html } from "lit";
import { customElement, property } from "lit/decorators.js";
import { when } from "lit/directives/when.js";

const elementName = "dc-partner-map-info-window";

@customElement(elementName)
export class MapInfoWindowPartnerElement extends LitElement {
  @property()
  name?: string;

  @property()
  country?: string;

  @property()
  partnership?: string;

  @property()
  logo?: string;

  @property()
  url?: string;

  render() {
    return html` <div id="detail">
      <div>
        ${when(this.logo, () => html`<img src=${this.logo!} />`)}
        <div>
          <p><strong>${this.name}</strong></p>
          <p>${this.country}</p>
        </div>
      </div>
      <div>
        <dc-badge backgroundColor=${getPartnershipColor(this.partnership)} textColor="var(--color-white)" small center
          >${this.partnership}</dc-badge
        >
        ${when(
          this.url,
          () => html`<p><a href=${this.url!}>Show partner</a></p>`
        )}
      </div>
    </div>`;
  }

  static styles = css`
    #detail {
      display: flex;
      flex-direction: column;
      gap: var(--unit-xs);
    }

    #detail > div {
      display: flex;
      gap: var(--unit-xs);
      align-items: center;
    }

    dc-badge {
      min-width: 60px;
    }

    img {
      width: 50px;
      align-self: flex-start;
    }

    p {
      margin: 0 0 0 auto;
    }
  `;
}

declare global {
  interface HTMLElementTagNameMap {
    [elementName]: MapInfoWindowPartnerElement;
  }
}

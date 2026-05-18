import { LitElement, html, css } from "lit";
import { customElement, state } from "lit/decorators.js";

@customElement("not-found-tracker-dashboard")
export class NotFoundTrackerDashboardElement extends LitElement {
  @state() private activeTab: "hits" | "rules" = "hits";

  static styles = css`
    :host {
      display: block;
      padding: var(--uui-size-space-4, 16px);
    }
    .tabs {
      display: flex;
      gap: var(--uui-size-space-2, 8px);
      border-bottom: 1px solid var(--uui-color-divider, #e9e9eb);
      margin-bottom: var(--uui-size-space-4, 16px);
    }
    .tab-btn {
      background: none;
      border: 0;
      padding: var(--uui-size-space-3, 12px) var(--uui-size-space-4, 16px);
      cursor: pointer;
      border-bottom: 2px solid transparent;
      font: inherit;
    }
    .tab-btn[aria-selected="true"] {
      border-bottom-color: var(--uui-color-selected, #3544b1);
      color: var(--uui-color-selected, #3544b1);
    }
  `;

  render() {
    return html`
      <div role="tablist" class="tabs">
        <button
          class="tab-btn"
          role="tab"
          aria-selected="${this.activeTab === "hits"}"
          @click=${() => (this.activeTab = "hits")}
        >
          Hits
        </button>
        <button
          class="tab-btn"
          role="tab"
          aria-selected="${this.activeTab === "rules"}"
          @click=${() => (this.activeTab = "rules")}
        >
          Ignore rules
        </button>
      </div>
      ${this.activeTab === "hits"
        ? html`<not-found-tracker-hits-tab></not-found-tracker-hits-tab>`
        : html`<not-found-tracker-ignore-rules-tab></not-found-tracker-ignore-rules-tab>`}
    `;
  }
}

export default NotFoundTrackerDashboardElement;

declare global {
  interface HTMLElementTagNameMap {
    "not-found-tracker-dashboard": NotFoundTrackerDashboardElement;
    "not-found-tracker-ignore-rules-tab": HTMLElement;
  }
}

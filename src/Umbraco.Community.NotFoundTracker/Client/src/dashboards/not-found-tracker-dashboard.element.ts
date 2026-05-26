import { LitElement, html, css } from "lit";
import { customElement, state } from "lit/decorators.js";
import { UmbElementMixin } from "@umbraco-cms/backoffice/element-api";
import { UMB_AUTH_CONTEXT } from "@umbraco-cms/backoffice/auth";
import { setAuthConfig } from "../api/not-found-tracker-api.js";

@customElement("not-found-tracker-dashboard")
export class NotFoundTrackerDashboardElement extends UmbElementMixin(LitElement) {
  @state() private activeTab: "hits" | "rules" = "hits";

  constructor() {
    super();
    this.consumeContext(UMB_AUTH_CONTEXT, (authContext) => {
      if (!authContext) return;
      setAuthConfig({
        token: () => authContext.getLatestToken(),
        baseUrl: authContext.getOpenApiConfiguration().base,
        credentials: authContext.getOpenApiConfiguration().credentials as RequestCredentials,
      });
    });
  }

  static styles = css`
    :host {
      display: block;
      padding: var(--uui-size-space-5, 20px);
    }
    uui-tab-group {
      margin-bottom: var(--uui-size-space-4, 16px);
    }
  `;

  render() {
    return html`
      <uui-tab-group>
        <uui-tab
          label="Hits"
          ?active=${this.activeTab === "hits"}
          @click=${() => (this.activeTab = "hits")}
        ></uui-tab>
        <uui-tab
          label="Ignore rules"
          ?active=${this.activeTab === "rules"}
          @click=${() => (this.activeTab = "rules")}
        ></uui-tab>
      </uui-tab-group>
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
  }
}

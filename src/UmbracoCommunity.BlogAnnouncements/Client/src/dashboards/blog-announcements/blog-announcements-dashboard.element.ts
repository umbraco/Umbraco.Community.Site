import { LitElement, html, css, customElement, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbElementMixin } from "@umbraco-cms/backoffice/element-api";
import { UMB_AUTH_CONTEXT } from "@umbraco-cms/backoffice/auth";
import { setAuthConfig } from "./api/blog-announcements-api.js";
import "./posts-tab.element.js";
import "./runs-tab.element.js";
import "./settings-tab.element.js";

type Tab = "posts" | "runs" | "settings";

@customElement("blog-announcements-dashboard")
export class BlogAnnouncementsDashboardElement extends UmbElementMixin(LitElement) {
  @state() private activeTab: Tab = "posts";

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
        <uui-tab label="Posts" ?active=${this.activeTab === "posts"} @click=${() => (this.activeTab = "posts")}></uui-tab>
        <uui-tab label="Runs" ?active=${this.activeTab === "runs"} @click=${() => (this.activeTab = "runs")}></uui-tab>
        <uui-tab label="Settings" ?active=${this.activeTab === "settings"} @click=${() => (this.activeTab = "settings")}></uui-tab>
      </uui-tab-group>
      ${this.activeTab === "posts"
        ? html`<blog-announcements-posts-tab></blog-announcements-posts-tab>`
        : this.activeTab === "runs"
          ? html`<blog-announcements-runs-tab></blog-announcements-runs-tab>`
          : html`<blog-announcements-settings-tab></blog-announcements-settings-tab>`}
    `;
  }
}

export default BlogAnnouncementsDashboardElement;

declare global {
  interface HTMLElementTagNameMap {
    "blog-announcements-dashboard": BlogAnnouncementsDashboardElement;
  }
}

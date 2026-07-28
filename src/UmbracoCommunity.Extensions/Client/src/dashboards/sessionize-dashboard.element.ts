import {
  LitElement,
  css,
  html,
  customElement,
  state,
} from "@umbraco-cms/backoffice/external/lit";
import { UmbElementMixin } from "@umbraco-cms/backoffice/element-api";
import { UMB_NOTIFICATION_CONTEXT } from "@umbraco-cms/backoffice/notification";
import { UMB_AUTH_CONTEXT } from "@umbraco-cms/backoffice/auth";
import { client } from "../api/client.gen.js";
import { UmbracoCommunityExtensionsService } from "../api/sdk.gen.js";

@customElement("sessionize-dashboard")
export class SessionizeDashboardElement extends UmbElementMixin(LitElement) {
  @state()
  private _isClearingCache = false;

  #notificationContext?: typeof UMB_NOTIFICATION_CONTEXT.TYPE;

  constructor() {
    super();

    this.consumeContext(UMB_NOTIFICATION_CONTEXT, (notificationContext) => {
      this.#notificationContext = notificationContext;
    });

    this.consumeContext(UMB_AUTH_CONTEXT, (authContext) => {
      const config = authContext?.getOpenApiConfiguration();
      client.setConfig({
        auth: config?.token ?? undefined,
        baseUrl: config?.base ?? "",
        credentials: config?.credentials ?? "same-origin",
      });
    });
  }

  #showNotification(color: "positive" | "warning" | "danger" | "default", headline: string, message?: string) {
    if (this.#notificationContext) {
      this.#notificationContext.peek(color, {
        data: {
          headline,
          message: message || "",
        },
      });
    }
  }

  async #onClearCache() {
    this._isClearingCache = true;

    try {
      const { data, error } = await UmbracoCommunityExtensionsService.refreshSessionizeCache();

      if (error) {
        throw new Error(String(error));
      }

      this.#showNotification(
        "positive",
        "Sessionize cache cleared",
        data?.message ?? "The local Sessionize cache has been cleared. Fresh data will be fetched on the next request."
      );
    } catch (error) {
      console.error("Failed to clear Sessionize cache", error);
      this.#showNotification("danger", "Failed to clear Sessionize cache", String(error));
    } finally {
      this._isClearingCache = false;
    }
  }

  render() {
    return html`
      <div class="container">
        <uui-box headline="Cache Management">
          <p class="description">
            Clear the local in-memory cache for Sessionize event data. Fresh data will be fetched from the
            Sessionize API on the next request. Note that Sessionize itself caches API responses for up to 5
            minutes, so changes made in the Sessionize dashboard may not appear immediately.
          </p>
          <uui-button
            look="primary"
            @click=${this.#onClearCache}
            ?disabled=${this._isClearingCache}
          >
            <uui-icon name="icon-sync"></uui-icon>
            ${this._isClearingCache ? "Clearing..." : "Clear Sessionize Cache"}
          </uui-button>
        </uui-box>
      </div>
    `;
  }

  static styles = [
    css`
      :host {
        display: block;
        padding: var(--uui-size-layout-1);
      }

      .container {
        max-width: 800px;
      }

      .description {
        color: var(--uui-color-text-alt);
        margin: 0 0 var(--uui-size-space-5) 0;
      }
    `,
  ];
}

export default SessionizeDashboardElement;

declare global {
  interface HTMLElementTagNameMap {
    "sessionize-dashboard": SessionizeDashboardElement;
  }
}

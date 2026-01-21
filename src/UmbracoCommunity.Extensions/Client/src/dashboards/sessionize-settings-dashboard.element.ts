import {
  LitElement,
  css,
  html,
  customElement,
  state,
} from "@umbraco-cms/backoffice/external/lit";
import { UmbElementMixin } from "@umbraco-cms/backoffice/element-api";
import { UUIButtonElement } from "@umbraco-cms/backoffice/external/uui";
import { UMB_NOTIFICATION_CONTEXT } from "@umbraco-cms/backoffice/notification";
import { UmbracoCommunityExtensionsService } from "../api/index.js";

@customElement("sessionize-settings-dashboard")
export class SessionizeSettingsDashboardElement extends UmbElementMixin(LitElement) {
  @state()
  private _lastRefreshed?: Date;

  @state()
  private _isRefreshing = false;

  #notificationContext?: typeof UMB_NOTIFICATION_CONTEXT.TYPE;

  constructor() {
    super();

    this.consumeContext(UMB_NOTIFICATION_CONTEXT, (notificationContext) => {
      this.#notificationContext = notificationContext;
    });
  }

  #onClickRefreshCache = async (ev: Event) => {
    const buttonElement = ev.target as UUIButtonElement;
    buttonElement.state = "waiting";
    this._isRefreshing = true;

    const { data, error } = await UmbracoCommunityExtensionsService.refreshSessionizeCache();

    if (error) {
      buttonElement.state = "failed";
      this._isRefreshing = false;
      console.error(error);

      this.#notificationContext?.peek("danger", {
        data: {
          headline: "Cache Refresh Failed",
          message: "Unable to refresh Sessionize cache. Please try again.",
        },
      });
      return;
    }

    if (data?.success) {
      this._lastRefreshed = data.refreshedAt ? new Date(data.refreshedAt) : new Date();
      buttonElement.state = "success";

      this.#notificationContext?.peek("positive", {
        data: {
          headline: "Cache Refreshed",
          message: data.message || "Sessionize data will be fetched fresh on the next request.",
        },
      });
    }

    this._isRefreshing = false;
  };

  render() {
    return html`
      <uui-box headline="Sessionize Cache">
        <div class="description">
          <p>
            Session data from Sessionize is cached for performance. If you've made changes
            in Sessionize and want to see them reflected immediately on the website,
            click the button below to clear the cache.
          </p>
          <p>
            Fresh data will be fetched from Sessionize on the next page request.
          </p>
        </div>

        <div class="actions">
          <uui-button
            color="positive"
            look="primary"
            @click="${this.#onClickRefreshCache}"
            ?disabled="${this._isRefreshing}"
          >
            <uui-icon name="icon-refresh"></uui-icon>
            Refresh Sessionize Cache
          </uui-button>
        </div>

        ${this._lastRefreshed
          ? html`
              <div class="last-refreshed">
                <uui-icon name="icon-check"></uui-icon>
                Last refreshed: ${this._lastRefreshed.toLocaleString()}
              </div>
            `
          : ""}
      </uui-box>
    `;
  }

  static styles = [
    css`
      :host {
        display: block;
        padding: var(--uui-size-layout-1);
      }

      uui-box {
        max-width: 600px;
      }

      .description {
        margin-bottom: var(--uui-size-space-4);
      }

      .description p {
        margin: 0 0 var(--uui-size-space-3) 0;
        color: var(--uui-color-text-alt);
      }

      .actions {
        margin-bottom: var(--uui-size-space-4);
      }

      uui-button uui-icon {
        margin-right: var(--uui-size-space-2);
      }

      .last-refreshed {
        display: flex;
        align-items: center;
        gap: var(--uui-size-space-2);
        padding: var(--uui-size-space-3);
        background: var(--uui-color-positive-standalone);
        color: var(--uui-color-positive-contrast);
        border-radius: var(--uui-border-radius);
        font-size: var(--uui-type-small-size);
      }
    `,
  ];
}

export default SessionizeSettingsDashboardElement;

declare global {
  interface HTMLElementTagNameMap {
    "sessionize-settings-dashboard": SessionizeSettingsDashboardElement;
  }
}

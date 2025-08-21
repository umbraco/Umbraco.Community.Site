import { fetch, LogService } from "@umbraco-community/services";
import { LitElement, css, html } from "lit";
import { customElement, property, state } from "lit/decorators.js";
import { when } from "lit/directives/when.js";
import {
  PurchaseFlowForm,
  PurchaseFlowArgs,
  PurchaseFlowLog,
} from "./entities";

const elementName = "dc-uaas-purchase-logger";

@customElement(elementName)
export class DcUaasPurchaseFlowLogger extends LitElement {
  @property({ type: Object })
  user?: PurchaseFlowForm;

  @property({ type: Object })
  project?: PurchaseFlowArgs;

  @property({ type: Object })
  log?: PurchaseFlowLog;

  @state()
  private _hasLogged = false;

  @state()
  private _isLogging = false;

  @state()
  private _message?: string;

  async #log() {
    if (this._hasLogged) return;
    if (!this.user?.name || !this.user.email) return;
    if (!this.project?.sku || !this.project.plan) return;
    if (!this.log?.reason) return;

    this._isLogging = true;
    this._message = "Creating a ticket for you...";

    const { error } = await fetch(
      LogService.logPurchase(
        this.user.name,
        this.user.email,
        this.project.sku,
        this.project.plan,
        this.log.reason
      )
    );

    if (error) {
      this._message = `Something went wrong: ${error}`;
      return;
    }

    this._hasLogged = true;
    this._message =
      "Success! We will be in touch once we have investigated the issue.";
  }

  render() {
    return html`<div class="uaas-logger">
      ${when(
        this.log?.description,
        () => html` <p>${this.log?.description}</p>`,
        () => html` <div>
          <p>
            Fear not! We are working on it. Right now, there are two options you
            can take: try refreshing the page and create your project again, or
            click the button below and we will let you know when we have fixed
            the issue:
          </p>

          ${when(
            !this._hasLogged && !this._isLogging,
            () => html` <p>
              <uui-button @click=${this.#log} look="primary" color="default">
                Notify me when the issue has been resolved
              </uui-button>
            </p>`
          )}
          ${when(
            this._isLogging || this._hasLogged,
            () => html` <p>${this._message}</p>`
          )}

          <p>
            <small
              >Note that this will create a ticket in our support system
              (Zendesk)</small
            >
          </p>
        </div>`
      )}
    </div>`;
  }

  static styles = css`
    p {
      text-align: center;
    }
  `;
}

declare global {
  interface HTMLElementTagMap {
    [elementName]: DcUaasPurchaseFlowLogger;
  }
}

import { LitElement, html, css, customElement, state, nothing } from "@umbraco-cms/backoffice/external/lit";
import { UmbElementMixin } from "@umbraco-cms/backoffice/element-api";
import { UMB_AUTH_CONTEXT } from "@umbraco-cms/backoffice/auth";
import { UMB_NOTIFICATION_CONTEXT } from "@umbraco-cms/backoffice/notification";
import type { UmbNotificationContext } from "@umbraco-cms/backoffice/notification";
import { ApiError, ProfileMatchReviewApi, setAuthConfig } from "./api/profile-match-review-api.js";
import type { MatchState, ProfileMatch } from "./api/profile-match-review-types.js";
import "./profile-activity-review-panel.element.js";

const PAGE_SIZE = 25;
// The tab that shows the activity members added by hand instead of a match list.
const ACTIVITY_TAB = "Activity";
type Tab = MatchState | typeof ACTIVITY_TAB;
const TABS: Array<{ state: Tab; label: string }> = [
  { state: "Held", label: "Held" },
  { state: "Confirmed", label: "Linked" },
  { state: "Rejected", label: "Rejected" },
  { state: ACTIVITY_TAB, label: "Added activity" },
];

@customElement("profile-match-review-dashboard")
export class ProfileMatchReviewDashboardElement extends UmbElementMixin(LitElement) {
  @state() private activeState: Tab = "Held";
  @state() private items: ProfileMatch[] = [];
  @state() private total = 0;
  @state() private loading = false;
  @state() private noAccess = false;
  @state() private error?: string;
  @state() private busyId?: number;

  #authReady = false;
  #notifications?: UmbNotificationContext;

  constructor() {
    super();
    this.consumeContext(UMB_AUTH_CONTEXT, (authContext) => {
      if (!authContext) return;
      setAuthConfig({
        token: () => authContext.getLatestToken(),
        baseUrl: authContext.getOpenApiConfiguration().base,
        credentials: authContext.getOpenApiConfiguration().credentials as RequestCredentials,
      });
      if (!this.#authReady) {
        this.#authReady = true;
        void this.#load(true);
      }
    });
    this.consumeContext(UMB_NOTIFICATION_CONTEXT, (context) => {
      this.#notifications = context;
    });
  }

  async #load(reset: boolean) {
    this.loading = true;
    this.error = undefined;
    try {
      const skip = reset ? 0 : this.items.length;
      if (this.activeState === ACTIVITY_TAB) return;
      const page = await ProfileMatchReviewApi.list(this.activeState, skip, PAGE_SIZE);
      this.items = reset ? page.items : [...this.items, ...page.items];
      this.total = page.total;
      this.noAccess = false;
    } catch (e) {
      if (e instanceof ApiError && e.status === 403) {
        this.noAccess = true;
      } else {
        this.error = e instanceof Error ? e.message : String(e);
      }
    } finally {
      this.loading = false;
    }
  }

  #selectTab(next: Tab) {
    if (next === this.activeState) return;
    this.activeState = next;
    this.items = [];
    this.total = 0;
    if (next !== ACTIVITY_TAB) {
      void this.#load(true);
    }
  }

  async #decide(match: ProfileMatch, decision: "link" | "reject") {
    this.busyId = match.id;
    try {
      if (decision === "link") {
        await ProfileMatchReviewApi.link(match.id);
      } else {
        await ProfileMatchReviewApi.reject(match.id);
      }
      this.#notifications?.peek("positive", {
        data: { message: decision === "link" ? "Match linked." : "Match rejected." },
      });
      await this.#load(true);
    } catch (e) {
      this.#notifications?.peek("danger", {
        data: { message: e instanceof Error ? e.message : String(e) },
      });
    } finally {
      this.busyId = undefined;
    }
  }

  static styles = css`
    :host {
      display: block;
      padding: var(--uui-size-space-5, 20px);
    }
    uui-tab-group {
      margin-bottom: var(--uui-size-space-4, 16px);
    }
    .match {
      margin-bottom: var(--uui-size-space-4, 16px);
    }
    .match-body {
      padding: var(--uui-size-space-4, 16px);
      display: grid;
      gap: var(--uui-size-space-3, 12px);
    }
    .source {
      word-break: break-all;
    }
    .meta {
      color: var(--uui-color-text-alt);
      font-size: var(--uui-type-small-size);
    }
    ul.signals {
      margin: 0;
      padding-left: var(--uui-size-space-5, 20px);
    }
    .actions {
      display: flex;
      gap: var(--uui-size-space-3, 12px);
    }
  `;

  #renderMatch(match: ProfileMatch) {
    const busy = this.busyId === match.id;
    return html`
      <uui-box class="match" headline="${match.displayName} (@${match.handle})">
        <div class="match-body">
          <div class="source"><a href=${match.sourceUrl} target="_blank" rel="noopener noreferrer">${match.sourceUrl}</a></div>
          <div class="meta">
            Confidence ${Math.round(match.confidence * 100)}% ·
            ${match.decidedBy === "Manual"
              ? `decided by ${match.decidedByUserName ?? "a reviewer"}`
              : "decided automatically"}
            · ${new Date(match.decidedUtc).toLocaleString()}
          </div>
          ${match.signals.length > 0
            ? html`<ul class="signals">
                ${match.signals.map((s) => html`<li><strong>${s.name}:</strong> ${s.detail}</li>`)}
              </ul>`
            : html`<div class="meta">No evidence recorded.</div>`}
          <div class="actions">
            ${match.state !== "Confirmed"
              ? html`<uui-button
                  look="primary"
                  color="positive"
                  label="Link anyway"
                  ?disabled=${busy}
                  @click=${() => this.#decide(match, "link")}
                ></uui-button>`
              : nothing}
            ${match.state !== "Rejected"
              ? html`<uui-button
                  look="primary"
                  color="danger"
                  label="Reject"
                  ?disabled=${busy}
                  @click=${() => this.#decide(match, "reject")}
                ></uui-button>`
              : nothing}
          </div>
        </div>
      </uui-box>
    `;
  }

  render() {
    if (this.noAccess) {
      return html`<uui-box headline="Profile match review">
        <p>This dashboard is for members of the Profile match review user group.</p>
      </uui-box>`;
    }

    return html`
      <uui-tab-group>
        ${TABS.map(
          (tab) => html`<uui-tab
            label=${tab.label}
            ?active=${this.activeState === tab.state}
            @click=${() => this.#selectTab(tab.state)}
          ></uui-tab>`,
        )}
      </uui-tab-group>
      ${this.activeState === ACTIVITY_TAB ? html`<profile-activity-review-panel></profile-activity-review-panel>` : this.#renderMatches()}
    `;
  }

  #renderMatches() {
    return html`
      ${this.error ? html`<uui-box><p>${this.error}</p></uui-box>` : nothing}
      ${!this.loading && !this.error && this.items.length === 0
        ? html`<uui-box><p>Nothing here.</p></uui-box>`
        : nothing}
      ${this.items.map((m) => this.#renderMatch(m))}
      ${this.loading ? html`<uui-loader></uui-loader>` : nothing}
      ${!this.loading && this.items.length < this.total
        ? html`<uui-button label="Show more" @click=${() => this.#load(false)}></uui-button>`
        : nothing}
    `;
  }
}

export default ProfileMatchReviewDashboardElement;

declare global {
  interface HTMLElementTagNameMap {
    "profile-match-review-dashboard": ProfileMatchReviewDashboardElement;
  }
}

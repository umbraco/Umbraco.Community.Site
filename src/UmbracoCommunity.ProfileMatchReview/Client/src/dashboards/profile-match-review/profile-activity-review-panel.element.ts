import { LitElement, html, css, customElement, state, nothing } from "@umbraco-cms/backoffice/external/lit";
import { UmbElementMixin } from "@umbraco-cms/backoffice/element-api";
import { UMB_NOTIFICATION_CONTEXT } from "@umbraco-cms/backoffice/notification";
import type { UmbNotificationContext } from "@umbraco-cms/backoffice/notification";
import { ApiError, ProfileMatchReviewApi } from "./api/profile-match-review-api.js";
import type { ActivityEntry, ActivityEntryState } from "./api/profile-match-review-types.js";

const PAGE_SIZE = 25;
const TABS: Array<{ state: ActivityEntryState; label: string }> = [
  { state: "Pending", label: "Waiting" },
  { state: "Verified", label: "Verified" },
  { state: "Rejected", label: "Rejected" },
];

/**
 * The activity members added to their profile by hand. A waiting entry is verified, or rejected with a reason the
 * member can read. The parent dashboard sets up the API auth before this is shown.
 */
@customElement("profile-activity-review-panel")
export class ProfileActivityReviewPanelElement extends UmbElementMixin(LitElement) {
  @state() private activeState: ActivityEntryState = "Pending";
  @state() private items: ActivityEntry[] = [];
  @state() private total = 0;
  @state() private loading = false;
  @state() private error?: string;
  @state() private busyId?: number;
  @state() private rejectingId?: number;
  @state() private reason = "";

  #notifications?: UmbNotificationContext;

  constructor() {
    super();
    this.consumeContext(UMB_NOTIFICATION_CONTEXT, (context) => {
      this.#notifications = context;
    });
  }

  connectedCallback() {
    super.connectedCallback();
    void this.#load(true);
  }

  async #load(reset: boolean) {
    this.loading = true;
    this.error = undefined;
    try {
      const skip = reset ? 0 : this.items.length;
      const page = await ProfileMatchReviewApi.listActivity(this.activeState, skip, PAGE_SIZE);
      this.items = reset ? page.items : [...this.items, ...page.items];
      this.total = page.total;
    } catch (e) {
      this.error = e instanceof ApiError || e instanceof Error ? e.message : String(e);
    } finally {
      this.loading = false;
    }
  }

  #selectTab(next: ActivityEntryState) {
    if (next === this.activeState) return;
    this.activeState = next;
    this.items = [];
    this.total = 0;
    this.rejectingId = undefined;
    void this.#load(true);
  }

  async #verify(entry: ActivityEntry) {
    await this.#decide(entry, () => ProfileMatchReviewApi.verifyActivity(entry.id), "Entry verified.");
  }

  async #reject(entry: ActivityEntry) {
    const reason = this.reason.trim();
    if (!reason) return;
    await this.#decide(entry, () => ProfileMatchReviewApi.rejectActivity(entry.id, reason), "Entry rejected.");
  }

  async #decide(entry: ActivityEntry, action: () => Promise<ActivityEntry>, message: string) {
    this.busyId = entry.id;
    try {
      await action();
      this.#notifications?.peek("positive", { data: { message } });
      this.rejectingId = undefined;
      this.reason = "";
      await this.#load(true);
    } catch (e) {
      this.#notifications?.peek("danger", { data: { message: e instanceof Error ? e.message : String(e) } });
    } finally {
      this.busyId = undefined;
    }
  }

  static styles = css`
    :host {
      display: block;
    }
    uui-tab-group {
      margin-bottom: var(--uui-size-space-4, 16px);
    }
    .entry {
      margin-bottom: var(--uui-size-space-4, 16px);
    }
    .entry-body {
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
    .description {
      margin: 0;
      white-space: pre-wrap;
    }
    .actions {
      display: flex;
      gap: var(--uui-size-space-3, 12px);
    }
    uui-textarea {
      width: 100%;
    }
  `;

  #renderEntry(entry: ActivityEntry) {
    const busy = this.busyId === entry.id;
    const rejecting = this.rejectingId === entry.id;
    return html`
      <uui-box class="entry" headline="${entry.title}">
        <div class="entry-body">
          <div class="meta">${entry.displayName} (@${entry.handle}) · ${entry.kindLabel} · ${entry.date}</div>
          <div class="source">
            <a href=${entry.url} target="_blank" rel="noopener noreferrer">${entry.url}</a>
          </div>
          ${entry.description ? html`<p class="description">${entry.description}</p>` : nothing}
          ${entry.state !== "Pending"
            ? html`<div class="meta">
                ${entry.state === "Verified" ? "Verified" : "Rejected"} by ${entry.decidedByUserName ?? "a reviewer"}
                ${entry.decidedUtc ? `· ${new Date(entry.decidedUtc).toLocaleString()}` : nothing}
                ${entry.rejectionReason ? html`<br />Reason: ${entry.rejectionReason}` : nothing}
              </div>`
            : html`<div class="meta">Submitted ${new Date(entry.createdUtc).toLocaleString()}</div>`}
          ${entry.state === "Rejected"
            ? html`<div class="actions">
                <uui-button
                  look="primary"
                  color="positive"
                  label="Verify anyway"
                  ?disabled=${busy}
                  @click=${() => this.#verify(entry)}
                ></uui-button>
              </div>`
            : nothing}
          ${entry.state === "Pending" || entry.state === "Verified"
            ? rejecting
              ? html`
                  <uui-textarea
                    label="Reason for the member"
                    placeholder="Why it can't be shown on their profile"
                    maxlength="500"
                    .value=${this.reason}
                    @input=${(e: Event) => (this.reason = (e.target as HTMLTextAreaElement).value)}
                  ></uui-textarea>
                  <div class="actions">
                    <uui-button
                      look="primary"
                      color="danger"
                      label="Reject with this reason"
                      ?disabled=${busy || !this.reason.trim()}
                      @click=${() => this.#reject(entry)}
                    ></uui-button>
                    <uui-button label="Cancel" @click=${() => (this.rejectingId = undefined)}></uui-button>
                  </div>
                `
              : html`<div class="actions">
                  ${entry.state === "Pending"
                    ? html`<uui-button
                        look="primary"
                        color="positive"
                        label="Verify"
                        ?disabled=${busy}
                        @click=${() => this.#verify(entry)}
                      ></uui-button>`
                    : nothing}
                  <uui-button
                    look="primary"
                    color="danger"
                    label="Reject"
                    ?disabled=${busy}
                    @click=${() => {
                      this.rejectingId = entry.id;
                      this.reason = "";
                    }}
                  ></uui-button>
                </div>`
            : nothing}
        </div>
      </uui-box>
    `;
  }

  render() {
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
      ${this.error ? html`<uui-box><p>${this.error}</p></uui-box>` : nothing}
      ${!this.loading && !this.error && this.items.length === 0 ? html`<uui-box><p>Nothing here.</p></uui-box>` : nothing}
      ${this.items.map((entry) => this.#renderEntry(entry))}
      ${this.loading ? html`<uui-loader></uui-loader>` : nothing}
      ${!this.loading && this.items.length < this.total
        ? html`<uui-button label="Show more" @click=${() => this.#load(false)}></uui-button>`
        : nothing}
    `;
  }
}

export default ProfileActivityReviewPanelElement;

declare global {
  interface HTMLElementTagNameMap {
    "profile-activity-review-panel": ProfileActivityReviewPanelElement;
  }
}

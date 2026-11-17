import { LitElement, html, css, customElement, state, nothing } from "@umbraco-cms/backoffice/external/lit";
import { UmbElementMixin } from "@umbraco-cms/backoffice/element-api";
import { UMB_NOTIFICATION_CONTEXT } from "@umbraco-cms/backoffice/notification";
import type { UmbNotificationContext } from "@umbraco-cms/backoffice/notification";
import { ApiError, ProfileMatchReviewApi } from "./api/profile-match-review-api.js";
import type { ActivityEntry, ActivityEntryState } from "./api/profile-match-review-types.js";

const PAGE_SIZE = 25;
const TABS: Array<{ state: ActivityEntryState; label: string }> = [
  { state: "Verified", label: "On profiles" },
  { state: "Rejected", label: "Removed" },
  // Only shown while legacy entries are still waiting (see #legacyWaiting).
  { state: "Pending", label: "Waiting (from before)" },
];

/**
 * The activity members added to their profile by hand. Entries go on the profile as soon as they are added, so this
 * lists them newest first for a moderator to remove what doesn't belong, with a note the member can read. Removed
 * entries can be restored. Entries added while they were still checked first, and never decided, are listed under
 * their own tab while there are any. The picture a member attached is shown too, since the entry's card shows it
 * publicly. The parent dashboard sets up the API auth before this is shown.
 */
@customElement("profile-activity-review-panel")
export class ProfileActivityReviewPanelElement extends UmbElementMixin(LitElement) {
  @state() private activeState: ActivityEntryState = "Verified";
  /** How many legacy entries are still waiting; their tab only shows while there are any. */
  @state() private legacyWaiting = 0;
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
    void this.#countLegacyWaiting();
  }

  async #countLegacyWaiting() {
    try {
      this.legacyWaiting = (await ProfileMatchReviewApi.listActivity("Pending", 0, 1)).total;
    } catch {
      // Only decides whether the tab shows; the main list reports errors.
    }
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

  async #show(entry: ActivityEntry) {
    const message = entry.state === "Rejected" ? "Entry restored to the member's profile." : "Entry shown on the member's profile.";
    await this.#decide(entry, () => ProfileMatchReviewApi.verifyActivity(entry.id), message);
  }

  async #remove(entry: ActivityEntry) {
    const reason = this.reason.trim();
    if (!reason) return;
    await this.#decide(entry, () => ProfileMatchReviewApi.rejectActivity(entry.id, reason), "Entry removed from the member's profile.");
  }

  async #decide(entry: ActivityEntry, action: () => Promise<ActivityEntry>, message: string) {
    this.busyId = entry.id;
    try {
      await action();
      this.#notifications?.peek("positive", { data: { message } });
      this.rejectingId = undefined;
      this.reason = "";
      await Promise.all([this.#load(true), this.#countLegacyWaiting()]);
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
    .picture {
      display: inline-block;
      justify-self: start;
    }
    .picture img {
      display: block;
      max-width: 240px;
      max-height: 160px;
      object-fit: contain;
      border: 1px solid var(--uui-color-border);
      border-radius: var(--uui-border-radius);
      background: var(--uui-color-surface-alt);
    }
    .actions {
      display: flex;
      gap: var(--uui-size-space-3, 12px);
    }
    uui-textarea {
      width: 100%;
    }
  `;

  /** When the entry was added and, when a moderator acted on it, who did what. */
  #renderStanding(entry: ActivityEntry) {
    const added = `Added ${new Date(entry.createdUtc).toLocaleString()}`;
    const by = entry.decidedByUserName ?? "a moderator";
    const when = entry.decidedUtc ? ` · ${new Date(entry.decidedUtc).toLocaleString()}` : "";
    switch (entry.state) {
      case "Rejected":
        return html`${added}<br />Removed by ${by}${when}
          ${entry.rejectionReason ? html`<br />Note for the member: ${entry.rejectionReason}` : nothing}`;
      case "Pending":
        return html`${added}, while entries were still checked first. Not on the profile until shown.`;
      default:
        return entry.decidedUtc ? html`${added}<br />Shown by ${by}${when}` : added;
    }
  }

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
          ${entry.imageUrl
            ? html`<a class="picture" href=${entry.imageUrl} target="_blank" rel="noopener noreferrer" title="Open the picture full size">
                <img src=${entry.imageUrl} alt="Picture attached by ${entry.displayName} for “${entry.title}”" loading="lazy" />
              </a>`
            : nothing}
          <div class="meta">${this.#renderStanding(entry)}</div>
          ${entry.state === "Rejected"
            ? html`<div class="actions">
                <uui-button
                  look="primary"
                  color="positive"
                  label="Restore to profile"
                  ?disabled=${busy}
                  @click=${() => this.#show(entry)}
                ></uui-button>
              </div>`
            : rejecting
              ? html`
                  <uui-textarea
                    label="Note for the member"
                    placeholder="Why it doesn't belong on their profile"
                    maxlength="500"
                    .value=${this.reason}
                    @input=${(e: Event) => (this.reason = (e.target as HTMLTextAreaElement).value)}
                  ></uui-textarea>
                  <div class="actions">
                    <uui-button
                      look="primary"
                      color="danger"
                      label="Remove with this note"
                      ?disabled=${busy || !this.reason.trim()}
                      @click=${() => this.#remove(entry)}
                    ></uui-button>
                    <uui-button label="Cancel" @click=${() => (this.rejectingId = undefined)}></uui-button>
                  </div>
                `
              : html`<div class="actions">
                  ${entry.state === "Pending"
                    ? html`<uui-button
                        look="primary"
                        color="positive"
                        label="Show on profile"
                        ?disabled=${busy}
                        @click=${() => this.#show(entry)}
                      ></uui-button>`
                    : nothing}
                  <uui-button
                    look="primary"
                    color="danger"
                    label="Remove from profile"
                    ?disabled=${busy}
                    @click=${() => {
                      this.rejectingId = entry.id;
                      this.reason = "";
                    }}
                  ></uui-button>
                </div>`}
        </div>
      </uui-box>
    `;
  }

  render() {
    return html`
      <uui-tab-group>
        ${TABS.filter((tab) => tab.state !== "Pending" || this.legacyWaiting > 0 || this.activeState === "Pending").map(
          (tab) => html`<uui-tab
            label=${tab.state === "Pending" ? `${tab.label} (${this.legacyWaiting})` : tab.label}
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

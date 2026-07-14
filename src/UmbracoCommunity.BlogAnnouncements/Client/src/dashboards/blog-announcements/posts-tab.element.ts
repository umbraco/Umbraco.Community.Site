import { LitElement, html, css, nothing, customElement, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbElementMixin } from "@umbraco-cms/backoffice/element-api";
import {
  UMB_MODAL_MANAGER_CONTEXT,
  type UmbModalManagerContext,
  umbConfirmModal,
} from "@umbraco-cms/backoffice/modal";
import { UMB_NOTIFICATION_CONTEXT } from "@umbraco-cms/backoffice/notification";
import { BlogAnnouncementsApi } from "./api/blog-announcements-api.js";
import type { PostListItem } from "./api/blog-announcements-types.js";
import { PostStatus } from "./api/blog-announcements-types.js";
import { POST_DETAILS_MODAL } from "./modals/post-details-modal.token.js";
import { STATUS_LABELS, statusColor, absoluteTime, relativeTime, parseServerDate } from "./blog-announcements.helpers.js";

const STATUS_OPTIONS = [
  { name: "All statuses", value: "" },
  { name: "Pending", value: String(PostStatus.Pending) },
  { name: "Announced", value: String(PostStatus.Announced) },
  { name: "Skipped (too old)", value: String(PostStatus.SkippedTooOld) },
  { name: "Suppressed", value: String(PostStatus.Suppressed) },
  { name: "Failed", value: String(PostStatus.Failed) },
];

function daysAgoIso(days: number): string {
  const d = new Date();
  d.setDate(d.getDate() - days);
  return d.toISOString().slice(0, 10);
}

@customElement("blog-announcements-posts-tab")
export class BlogAnnouncementsPostsTabElement extends UmbElementMixin(LitElement) {
  @state() private items: PostListItem[] = [];
  @state() private total = 0;
  @state() private loading = false;
  @state() private error: string | null = null;

  @state() private statusFilter = "";
  @state() private search = "";
  // Default view: last 30 days.
  @state() private fromDate = daysAgoIso(30);
  @state() private toDate = "";
  @state() private skip = 0;
  @state() private take = 25;

  // Posts currently mid-delivery — disables their row actions.
  @state() private busyIds = new Set<string>();

  private _modalManager?: UmbModalManagerContext;
  private _notifications?: typeof UMB_NOTIFICATION_CONTEXT.TYPE;

  constructor() {
    super();
    this.consumeContext(UMB_MODAL_MANAGER_CONTEXT, (ctx) => (this._modalManager = ctx));
    this.consumeContext(UMB_NOTIFICATION_CONTEXT, (ctx) => (this._notifications = ctx));
  }

  static styles = css`
    :host { display: block; }
    .toolbar {
      display: flex;
      gap: var(--uui-size-space-3);
      align-items: end;
      flex-wrap: wrap;
      margin-bottom: var(--uui-size-space-4);
    }
    .field { display: flex; flex-direction: column; gap: var(--uui-size-1); min-width: 150px; }
    .field > uui-input, .field > uui-select { width: 100%; }
    .spacer { flex: 1; }
    .post-cell { display: flex; flex-direction: column; gap: 2px; }
    .post-title a { color: var(--uui-color-interactive); text-decoration: none; font-weight: 600; }
    .post-title a:hover { text-decoration: underline; }
    .author { display: flex; align-items: center; gap: var(--uui-size-space-2); color: var(--uui-color-text-alt); font-size: 0.9em; }
    .author img { width: 20px; height: 20px; border-radius: 50%; object-fit: cover; }
    .row-actions { display: flex; gap: var(--uui-size-space-1); flex-wrap: wrap; }
    .status-cell uui-tag { text-transform: none; }
    .empty { text-align: center; padding: var(--uui-size-space-6); color: var(--uui-color-text-alt); }
    .pagination { display: flex; gap: var(--uui-size-space-3); align-items: center; margin-top: var(--uui-size-space-4); }
    .error-banner {
      color: var(--uui-color-danger);
      background: var(--uui-color-danger-emphasis, #fde7e7);
      padding: var(--uui-size-space-3);
      border-radius: var(--uui-border-radius);
      margin-bottom: var(--uui-size-space-3);
    }
  `;

  connectedCallback() {
    super.connectedCallback();
    this.load();
  }

  private async load() {
    this.loading = true;
    this.error = null;
    try {
      const result = await BlogAnnouncementsApi.listPosts({
        status: this.statusFilter === "" ? undefined : Number(this.statusFilter),
        search: this.search || undefined,
        from: this.fromDate ? new Date(this.fromDate).toISOString() : undefined,
        to: this.toDate ? new Date(`${this.toDate}T23:59:59`).toISOString() : undefined,
        skip: this.skip,
        take: this.take,
      });
      this.items = result.items;
      this.total = result.total;
    } catch (e) {
      this.error = (e as Error).message;
    } finally {
      this.loading = false;
    }
  }

  private notify(color: "positive" | "warning" | "danger", headline: string, message = "") {
    this._notifications?.peek(color, { data: { headline, message } });
  }

  private setBusy(id: string, busy: boolean) {
    if (busy) this.busyIds.add(id);
    else this.busyIds.delete(id);
    this.busyIds = new Set(this.busyIds);
  }

  private async openDetails(item: PostListItem) {
    const modal = this._modalManager?.open(this, POST_DETAILS_MODAL, { data: { sphereId: item.sphereId } });
    try {
      await modal?.onSubmit();
    } catch {
      // closed without submit
    }
    // The modal's "Mark as not announced" action can change the post's status — refresh the list.
    this.load();
  }

  private async repost(item: PostListItem) {
    try {
      await umbConfirmModal(this, {
        headline: "Repost to Discord?",
        content: "This will post to Discord again — sure?",
        confirmLabel: "Repost",
      });
    } catch {
      return; // cancelled
    }
    await this.deliver(item, "Repost");
  }

  private async postNow(item: PostListItem) {
    try {
      await umbConfirmModal(this, {
        headline: "Post to Discord now?",
        content: "This post was never announced. This will post it to Discord.",
        confirmLabel: "Post now",
      });
    } catch {
      return; // cancelled
    }
    await this.deliver(item, "PostNow");
  }

  private async deliver(item: PostListItem, trigger: "Repost" | "PostNow") {
    this.setBusy(item.sphereId, true);
    try {
      const result = await BlogAnnouncementsApi.announce(item.sphereId, trigger);
      if (result.dryRun) {
        this.notify("warning", "Dry-run: nothing posted", "Dry-run mode is on — the payload was logged but not sent to Discord.");
      } else if (result.outcome === "Success") {
        this.notify("positive", "Posted to Discord");
      } else {
        this.notify("danger", "Delivery failed", result.httpStatus ? `Discord returned ${result.httpStatus}.` : "The delivery did not succeed.");
      }
      await this.load();
    } catch (e) {
      this.notify("danger", "Delivery failed", (e as Error).message);
    } finally {
      this.setBusy(item.sphereId, false);
    }
  }

  render() {
    const statusOptions = STATUS_OPTIONS.map((o) => ({ ...o, selected: o.value === this.statusFilter }));

    return html`
      <div class="toolbar">
        <div class="field">
          <uui-label for="filter-status">Status</uui-label>
          <uui-select
            id="filter-status"
            .options=${statusOptions}
            @change=${(e: Event) => {
              this.statusFilter = (e.target as HTMLSelectElement).value;
              this.skip = 0;
              this.load();
            }}
          ></uui-select>
        </div>
        <div class="field">
          <uui-label for="filter-search">Search</uui-label>
          <uui-input
            id="filter-search"
            type="search"
            placeholder="Title or author…"
            .value=${this.search}
            @input=${(e: Event) => (this.search = (e.target as HTMLInputElement).value)}
            @change=${() => { this.skip = 0; this.load(); }}
          ></uui-input>
        </div>
        <div class="field">
          <uui-label for="filter-from">From</uui-label>
          <uui-input
            id="filter-from"
            type="date"
            .value=${this.fromDate}
            @change=${(e: Event) => { this.fromDate = (e.target as HTMLInputElement).value; this.skip = 0; this.load(); }}
          ></uui-input>
        </div>
        <div class="field">
          <uui-label for="filter-to">To</uui-label>
          <uui-input
            id="filter-to"
            type="date"
            .value=${this.toDate}
            @change=${(e: Event) => { this.toDate = (e.target as HTMLInputElement).value; this.skip = 0; this.load(); }}
          ></uui-input>
        </div>
        <div class="spacer"></div>
        <uui-button
          look="secondary"
          label=${this.loading ? "Refreshing…" : "Refresh"}
          ?disabled=${this.loading}
          @click=${() => this.load()}
        ></uui-button>
      </div>

      ${this.error ? html`<div class="error-banner">${this.error}</div>` : nothing}
      ${this.loading ? html`<uui-loader></uui-loader>` : nothing}

      <uui-table>
        <uui-table-head>
          <uui-table-head-cell>Post</uui-table-head-cell>
          <uui-table-head-cell>Published</uui-table-head-cell>
          <uui-table-head-cell>Status</uui-table-head-cell>
          <uui-table-head-cell>Announced</uui-table-head-cell>
          <uui-table-head-cell></uui-table-head-cell>
        </uui-table-head>
        ${this.items.length === 0 && !this.loading
          ? html`
              <uui-table-row>
                <uui-table-cell colspan="5"><div class="empty">No posts in range.</div></uui-table-cell>
              </uui-table-row>
            `
          : this.items.map((item) => this.renderRow(item))}
      </uui-table>

      <div class="pagination">
        <uui-button
          look="secondary"
          label="‹ Prev"
          ?disabled=${this.skip === 0}
          @click=${() => { this.skip = Math.max(0, this.skip - this.take); this.load(); }}
        ></uui-button>
        <span>${this.total === 0 ? 0 : this.skip + 1}-${Math.min(this.skip + this.take, this.total)} of ${this.total}</span>
        <uui-button
          look="secondary"
          label="Next ›"
          ?disabled=${this.skip + this.take >= this.total}
          @click=${() => { this.skip += this.take; this.load(); }}
        ></uui-button>
      </div>
    `;
  }

  private renderRow(item: PostListItem) {
    const busy = this.busyIds.has(item.sphereId);
    const canPostNow = item.status === PostStatus.Pending || item.status === PostStatus.SkippedTooOld;
    return html`
      <uui-table-row>
        <uui-table-cell>
          <div class="post-cell">
            <span class="post-title">
              <a href=${item.url} target="_blank" rel="noopener noreferrer">${item.title}</a>
            </span>
            <span class="author">
              ${item.authorAvatarUrl
                ? html`<img src=${item.authorAvatarUrl} alt="" referrerpolicy="no-referrer" />`
                : nothing}
              ${item.authorName ?? "Unknown author"}
            </span>
          </div>
        </uui-table-cell>
        <uui-table-cell title=${absoluteTime(item.publishedAtUtc)}>
          ${parseServerDate(item.publishedAtUtc).toLocaleDateString()}
        </uui-table-cell>
        <uui-table-cell class="status-cell">
          <uui-tag color=${statusColor(item.status)} look="primary">${STATUS_LABELS[item.status] ?? item.status}</uui-tag>
        </uui-table-cell>
        <uui-table-cell title=${absoluteTime(item.announcedUtc)}>
          ${item.announcedUtc ? relativeTime(item.announcedUtc) : html`<span class="empty">—</span>`}
        </uui-table-cell>
        <uui-table-cell>
          <div class="row-actions">
            ${canPostNow
              ? html`<uui-button compact look="primary" label="Post now" ?disabled=${busy} @click=${() => this.postNow(item)}></uui-button>`
              : html`<uui-button compact look="secondary" label="Repost" ?disabled=${busy} @click=${() => this.repost(item)}></uui-button>`}
            <uui-button compact look="secondary" label="Details" @click=${() => this.openDetails(item)}></uui-button>
          </div>
        </uui-table-cell>
      </uui-table-row>
    `;
  }
}

declare global {
  interface HTMLElementTagNameMap {
    "blog-announcements-posts-tab": BlogAnnouncementsPostsTabElement;
  }
}

import { html, css, nothing, customElement, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbModalBaseElement, umbConfirmModal } from "@umbraco-cms/backoffice/modal";
import { UMB_NOTIFICATION_CONTEXT } from "@umbraco-cms/backoffice/notification";
import { BlogAnnouncementsApi } from "../api/blog-announcements-api.js";
import { PostStatus } from "../api/blog-announcements-types.js";
import type { PostDetail } from "../api/blog-announcements-types.js";
import type { PostDetailsModalData, PostDetailsModalValue } from "./post-details-modal.token.js";
import { STATUS_LABELS, statusColor, TRIGGER_LABELS, absoluteTime, relativeTime } from "../blog-announcements.helpers.js";

@customElement("blog-announcements-post-details-modal")
export class BlogAnnouncementsPostDetailsModalElement extends UmbModalBaseElement<
  PostDetailsModalData,
  PostDetailsModalValue
> {
  @state() private detail: PostDetail | null = null;
  @state() private loading = false;
  @state() private resetting = false;
  @state() private error: string | null = null;

  static styles = css`
    .embed {
      border-left: 4px solid var(--uui-color-interactive, #3544b1);
      background: var(--uui-color-surface-alt, #f4f4f5);
      border-radius: 6px;
      padding: var(--uui-size-space-4);
      margin-bottom: var(--uui-size-space-5);
    }
    .embed-author {
      display: flex;
      align-items: center;
      gap: var(--uui-size-space-2);
      margin-bottom: var(--uui-size-space-2);
      font-size: 0.9em;
      color: var(--uui-color-text-alt);
    }
    .embed-author img {
      width: 24px;
      height: 24px;
      border-radius: 50%;
      object-fit: cover;
    }
    .embed-title {
      font-weight: 700;
      color: var(--uui-color-interactive, #3544b1);
      margin: 0 0 var(--uui-size-space-2) 0;
    }
    .embed-title a { color: inherit; text-decoration: none; }
    .embed-title a:hover { text-decoration: underline; }
    .embed-desc { margin: 0 0 var(--uui-size-space-3) 0; }
    .embed-cover {
      max-width: 100%;
      border-radius: 4px;
      display: block;
    }
    .meta {
      display: grid;
      grid-template-columns: max-content 1fr;
      column-gap: var(--uui-size-space-4);
      row-gap: var(--uui-size-space-2);
      margin: 0 0 var(--uui-size-space-5) 0;
      word-break: break-word;
    }
    .meta dt { font-weight: 600; color: var(--uui-color-text-alt); }
    .meta dd { margin: 0; }
    .section-title { font-weight: 600; margin: 0 0 var(--uui-size-space-3) 0; }
    .empty { color: var(--uui-color-text-alt); font-style: italic; }
    .error { color: var(--uui-color-danger); }
  `;

  connectedCallback() {
    super.connectedCallback();
    this.load();
  }

  private async load() {
    if (!this.data) return;
    this.loading = true;
    this.error = null;
    try {
      this.detail = await BlogAnnouncementsApi.getPost(this.data.sphereId);
    } catch (e) {
      this.error = (e as Error).message;
    } finally {
      this.loading = false;
    }
  }

  /**
   * Testing aid: resets the post to "not announced" (Pending, AnnouncedUtc cleared) so the
   * automatic cycle can pick it up again. History is kept — a "Reset" attempt row is appended.
   */
  private async resetAnnounced() {
    if (!this.data) return;
    try {
      await umbConfirmModal(this, {
        headline: "Mark as not announced?",
        content: "This will mark the post as not announced so it can be posted again automatically — sure?",
        confirmLabel: "Mark as not announced",
      });
    } catch {
      return; // cancelled
    }

    this.resetting = true;
    this.error = null;
    try {
      await BlogAnnouncementsApi.resetPost(this.data.sphereId);
      const notifications = await this.getContext(UMB_NOTIFICATION_CONTEXT);
      notifications?.peek("positive", {
        data: { headline: "Marked as not announced", message: "The post is pending again and eligible for the next automatic cycle." },
      });
      await this.load();
    } catch (e) {
      this.error = (e as Error).message;
    } finally {
      this.resetting = false;
    }
  }

  render() {
    return html`
      <umb-body-layout headline="Post details">
        <uui-box>
          ${this.error ? html`<div class="error">${this.error}</div>` : nothing}
          ${this.loading
            ? html`<uui-loader></uui-loader>`
            : this.detail
              ? this.renderDetail(this.detail)
              : nothing}
        </uui-box>
        ${this.detail && (this.detail.status === PostStatus.Announced || this.detail.status === PostStatus.Failed)
          ? html`
              <uui-button
                slot="actions"
                look="secondary"
                color="warning"
                label=${this.resetting ? "Resetting…" : "Mark as not announced"}
                ?disabled=${this.resetting}
                @click=${() => this.resetAnnounced()}
              ></uui-button>
            `
          : nothing}
        <uui-button
          slot="actions"
          look="secondary"
          label="Close"
          @click=${() => this._submitModal()}
        ></uui-button>
      </umb-body-layout>
    `;
  }

  private renderDetail(d: PostDetail) {
    const outcomeOutlives = d.attempts.length > 0;
    return html`
      <div class="embed">
        <div class="embed-author">
          ${d.authorAvatarUrl
            ? html`<img src=${d.authorAvatarUrl} alt="" referrerpolicy="no-referrer" />`
            : nothing}
          <span>${d.authorName ?? "Umbraco Community"}</span>
        </div>
        <p class="embed-title">
          <a href=${d.url} target="_blank" rel="noopener noreferrer">${d.title}</a>
        </p>
        ${d.excerpt ? html`<p class="embed-desc">${d.excerpt}</p>` : nothing}
        ${d.coverImageUrl
          ? html`<img class="embed-cover" src=${d.coverImageUrl} alt="" referrerpolicy="no-referrer" />`
          : nothing}
      </div>

      <dl class="meta">
        <dt>Status</dt>
        <dd>
          <uui-tag color=${statusColor(d.status)} look="primary">${STATUS_LABELS[d.status] ?? d.status}</uui-tag>
        </dd>
        <dt>Sphere id</dt>
        <dd>${d.sphereId}</dd>
        <dt>URL</dt>
        <dd><a href=${d.url} target="_blank" rel="noopener noreferrer">${d.url}</a></dd>
        <dt>Published</dt>
        <dd>${absoluteTime(d.publishedAtUtc)}</dd>
        <dt>First seen</dt>
        <dd>${absoluteTime(d.firstSeenUtc)}</dd>
        <dt>Announced</dt>
        <dd>${d.announcedUtc ? absoluteTime(d.announcedUtc) : html`<span class="empty">Not announced</span>`}</dd>
        <dt>Fingerprint</dt>
        <dd>${d.fingerprint}</dd>
      </dl>

      <h4 class="section-title">
        Attempt history${outcomeOutlives ? html` (${d.attempts.length})` : nothing}
      </h4>
      ${d.attempts.length === 0
        ? html`<div class="empty">No delivery attempts yet.</div>`
        : html`
          <uui-table>
            <uui-table-head>
              <uui-table-head-cell>When</uui-table-head-cell>
              <uui-table-head-cell>Outcome</uui-table-head-cell>
              <uui-table-head-cell>HTTP</uui-table-head-cell>
              <uui-table-head-cell>Trigger</uui-table-head-cell>
            </uui-table-head>
            ${d.attempts.map(
              (a) => html`
                <uui-table-row>
                  <uui-table-cell title=${absoluteTime(a.attemptedUtc)}>${relativeTime(a.attemptedUtc)}</uui-table-cell>
                  <uui-table-cell>${a.outcome}</uui-table-cell>
                  <uui-table-cell>${a.httpStatus ?? "—"}</uui-table-cell>
                  <uui-table-cell>${TRIGGER_LABELS[a.trigger] ?? a.trigger}</uui-table-cell>
                </uui-table-row>
              `,
            )}
          </uui-table>
        `}
    `;
  }
}

export default BlogAnnouncementsPostDetailsModalElement;

declare global {
  interface HTMLElementTagNameMap {
    "blog-announcements-post-details-modal": BlogAnnouncementsPostDetailsModalElement;
  }
}

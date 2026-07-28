import { LitElement, html, css, nothing, customElement, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbElementMixin } from "@umbraco-cms/backoffice/element-api";
import { BlogAnnouncementsApi } from "./api/blog-announcements-api.js";
import type { RunListItem } from "./api/blog-announcements-types.js";
import { absoluteTime, relativeTime } from "./blog-announcements.helpers.js";

@customElement("blog-announcements-runs-tab")
export class BlogAnnouncementsRunsTabElement extends UmbElementMixin(LitElement) {
  @state() private items: RunListItem[] = [];
  @state() private total = 0;
  @state() private loading = false;
  @state() private error: string | null = null;
  @state() private skip = 0;
  @state() private take = 25;

  static styles = css`
    :host { display: block; }
    .empty { text-align: center; padding: var(--uui-size-space-6); color: var(--uui-color-text-alt); }
    .pagination { display: flex; gap: var(--uui-size-space-3); align-items: center; margin-top: var(--uui-size-space-4); }
    .toolbar { display: flex; justify-content: flex-end; margin-bottom: var(--uui-size-space-4); }
    .error-banner {
      color: var(--uui-color-danger);
      background: var(--uui-color-danger-emphasis, #fde7e7);
      padding: var(--uui-size-space-3);
      border-radius: var(--uui-border-radius);
      margin-bottom: var(--uui-size-space-3);
    }
    /* Left-aligned like the headers (and the Posts tab); tabular digits keep columns tidy. */
    .num { font-variant-numeric: tabular-nums; }
    .failed { color: var(--uui-color-danger); font-weight: 600; }
  `;

  connectedCallback() {
    super.connectedCallback();
    this.load();
  }

  private async load() {
    this.loading = true;
    this.error = null;
    try {
      const result = await BlogAnnouncementsApi.listRuns(this.skip, this.take);
      this.items = result.items;
      this.total = result.total;
    } catch (e) {
      this.error = (e as Error).message;
    } finally {
      this.loading = false;
    }
  }

  render() {
    return html`
      <div class="toolbar">
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
          <uui-table-head-cell>Run</uui-table-head-cell>
          <uui-table-head-cell>Fetched</uui-table-head-cell>
          <uui-table-head-cell>New</uui-table-head-cell>
          <uui-table-head-cell>Announced</uui-table-head-cell>
          <uui-table-head-cell>Skipped</uui-table-head-cell>
          <uui-table-head-cell>Failed</uui-table-head-cell>
          <uui-table-head-cell>Mode</uui-table-head-cell>
        </uui-table-head>
        ${this.items.length === 0 && !this.loading
          ? html`
              <uui-table-row>
                <uui-table-cell colspan="7"><div class="empty">No runs recorded yet.</div></uui-table-cell>
              </uui-table-row>
            `
          : this.items.map(
              (r) => html`
                <uui-table-row>
                  <uui-table-cell title=${absoluteTime(r.runUtc)}>${relativeTime(r.runUtc)}</uui-table-cell>
                  <uui-table-cell class="num">${r.fetched}</uui-table-cell>
                  <uui-table-cell class="num">${r.new}</uui-table-cell>
                  <uui-table-cell class="num">${r.announced}</uui-table-cell>
                  <uui-table-cell class="num">${r.skipped}</uui-table-cell>
                  <uui-table-cell class="num ${r.failed > 0 ? "failed" : ""}">${r.failed}</uui-table-cell>
                  <uui-table-cell>
                    <uui-tag color=${r.dryRun ? "default" : "positive"} look="primary">
                      ${r.dryRun ? "Dry-run" : "Live"}
                    </uui-tag>
                  </uui-table-cell>
                </uui-table-row>
              `,
            )}
      </uui-table>

      <div class="pagination">
        <uui-button look="secondary" label="‹ Prev" ?disabled=${this.skip === 0}
          @click=${() => { this.skip = Math.max(0, this.skip - this.take); this.load(); }}></uui-button>
        <span>${this.total === 0 ? 0 : this.skip + 1}-${Math.min(this.skip + this.take, this.total)} of ${this.total}</span>
        <uui-button look="secondary" label="Next ›" ?disabled=${this.skip + this.take >= this.total}
          @click=${() => { this.skip += this.take; this.load(); }}></uui-button>
      </div>
    `;
  }
}

declare global {
  interface HTMLElementTagNameMap {
    "blog-announcements-runs-tab": BlogAnnouncementsRunsTabElement;
  }
}

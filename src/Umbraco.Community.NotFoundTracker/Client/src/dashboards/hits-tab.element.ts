import { LitElement, html, css, nothing } from "lit";
import { customElement, state } from "lit/decorators.js";
import { UmbElementMixin } from "@umbraco-cms/backoffice/element-api";
import { UMB_MODAL_MANAGER_CONTEXT, type UmbModalManagerContext, umbConfirmModal } from "@umbraco-cms/backoffice/modal";
import { UMB_DOCUMENT_PICKER_MODAL } from "@umbraco-cms/backoffice/document";
import { NotFoundTrackerApi } from "../api/not-found-tracker-api.js";
import type { HitListItem, HitListResponse } from "../api/types.js";
import { ADD_IGNORE_RULE_MODAL } from "./modals/add-ignore-rule-modal.token.js";
import { HIT_DETAILS_MODAL } from "./modals/hit-details-modal.token.js";

const STATUS_LABELS = ["Active", "Ignored", "Redirected"];
const SORTS = [
  { value: "0", name: "Recently seen" },
  { value: "1", name: "Popularity" },
  { value: "2", name: "First seen" },
  { value: "3", name: "Query strings" },
];

@customElement("not-found-tracker-hits-tab")
export class HitsTabElement extends UmbElementMixin(LitElement) {
  @state() private items: HitListItem[] = [];
  @state() private total = 0;
  @state() private loading = false;
  @state() private error: string | null = null;

  @state() private hostnames: string[] = [];
  @state() private hostnameFilter = "";
  @state() private statusFilter: number = 0;
  @state() private search = "";
  @state() private sort: number = 0;
  @state() private skip = 0;
  @state() private take = 25;

  @state() private selectedIds = new Set<number>();

  private _modalManager?: UmbModalManagerContext;

  constructor() {
    super();
    this.consumeContext(UMB_MODAL_MANAGER_CONTEXT, (ctx) => {
      this._modalManager = ctx;
    });
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
    .field { display: flex; flex-direction: column; gap: var(--uui-size-1); min-width: 160px; }
    .field > uui-input,
    .field > uui-select { width: 100%; }
    .spacer { flex: 1; }
    .row-actions { display: flex; gap: var(--uui-size-space-1); }
    .status-cell uui-tag { text-transform: none; }
    .checkbox-cell { width: 1%; white-space: nowrap; }
    .path-link {
      color: var(--uui-color-interactive);
      text-decoration: none;
    }
    .path-link:hover { text-decoration: underline; }
    .empty {
      text-align: center;
      padding: var(--uui-size-space-6);
      color: var(--uui-color-text-alt);
    }
    .pagination {
      display: flex;
      gap: var(--uui-size-space-3);
      align-items: center;
      margin-top: var(--uui-size-space-4);
    }
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
    this.loadHostnames();
  }

  private async loadHostnames() {
    try {
      this.hostnames = await NotFoundTrackerApi.getHostnames();
    } catch {
      // Non-fatal — dropdown stays empty if this fails.
    }
  }

  private async load() {
    this.loading = true;
    this.error = null;
    try {
      const result: HitListResponse = await NotFoundTrackerApi.listHits({
        hostname: this.hostnameFilter || undefined,
        status: this.statusFilter,
        search: this.search || undefined,
        sort: this.sort,
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

  private toggleSelect(id: number) {
    if (this.selectedIds.has(id)) this.selectedIds.delete(id);
    else this.selectedIds.add(id);
    this.requestUpdate();
  }

  private toggleSelectAll() {
    if (this.selectedIds.size === this.items.length) {
      this.selectedIds.clear();
    } else {
      this.selectedIds = new Set(this.items.map((i) => i.id));
    }
    this.requestUpdate();
  }

  private async deleteSelected() {
    if (this.selectedIds.size === 0) return;
    const count = this.selectedIds.size;
    try {
      await umbConfirmModal(this, {
        headline: `Delete ${count} hit${count === 1 ? "" : "s"}?`,
        content: html`
          <p>This removes the selected row${count === 1 ? "" : "s"} from the 404 tracker.</p>
          <p>
            <strong>It does not ignore the URL${count === 1 ? "" : "s"}</strong> — if the same path
            is hit again, it'll reappear in the list. To stop tracking a URL, use
            <em>Ignore</em> instead.
          </p>
        `,
        color: "danger",
        confirmLabel: count === 1 ? "Delete hit" : `Delete ${count} hits`,
      });
    } catch {
      return; // cancelled
    }
    await NotFoundTrackerApi.bulkDeleteHits([...this.selectedIds]);
    this.selectedIds.clear();
    await this.load();
  }

  private async deleteOne(item: HitListItem) {
    try {
      await umbConfirmModal(this, {
        headline: "Delete this hit?",
        content: html`
          <p>This removes <strong>${item.path}</strong> from the 404 tracker.</p>
          <p>
            <strong>It does not ignore the URL</strong> — if the same path is hit again, it'll
            reappear in the list. To stop tracking it, use <em>Ignore</em> instead.
          </p>
        `,
        color: "danger",
        confirmLabel: "Delete hit",
      });
    } catch {
      return; // cancelled
    }
    await NotFoundTrackerApi.deleteHit(item.id);
    await this.load();
  }

  private openDetails(item: HitListItem) {
    if (!this._modalManager) return;
    this._modalManager.open(this, HIT_DETAILS_MODAL, { data: { hitId: item.id } });
  }

  private async openIgnoreModal(item: HitListItem) {
    if (!this._modalManager) return;
    const modal = this._modalManager.open(this, ADD_IGNORE_RULE_MODAL, {
      data: {
        hitId: item.id,
        suggestedPath: item.path,
        suggestedHostname: item.hostname,
      },
    });
    const submitted = await modal.onSubmit().then(() => true).catch(() => false);
    if (submitted) await this.load();
  }

  private async openRedirectPicker(item: HitListItem) {
    if (!this._modalManager) return;
    const modal = this._modalManager.open(this, UMB_DOCUMENT_PICKER_MODAL, {
      data: { multiple: false },
    });
    const result = await modal.onSubmit().catch(() => null);
    const targetKey = result?.selection?.[0];
    if (!targetKey) return;

    try {
      await NotFoundTrackerApi.createRedirect(item.id, targetKey, null);
      await this.load();
    } catch (e) {
      this.error = `Failed to create redirect: ${(e as Error).message}`;
    }
  }

  private statusColor(status: number): "default" | "positive" | "warning" {
    switch (status) {
      case 1: return "warning"; // Ignored
      case 2: return "positive"; // Redirected
      default: return "default"; // Active
    }
  }

  render() {
    const hostnameOptions = [
      { name: "All sites", value: "", selected: this.hostnameFilter === "" },
      ...this.hostnames.map((h) => ({ name: h, value: h, selected: this.hostnameFilter === h })),
    ];
    const statusOptions = [
      { name: "Active", value: "0", selected: this.statusFilter === 0 },
      { name: "Redirected", value: "2", selected: this.statusFilter === 2 },
    ];
    const sortOptions = SORTS.map((s) => ({ ...s, selected: String(this.sort) === s.value }));
    const allSelected = this.items.length > 0 && this.selectedIds.size === this.items.length;

    return html`
      <div class="toolbar">
        <div class="field">
          <uui-label>Hostname</uui-label>
          <uui-select
            .options=${hostnameOptions}
            @change=${(e: Event) => {
              this.hostnameFilter = (e.target as HTMLSelectElement).value;
              this.skip = 0;
              this.load();
            }}
          ></uui-select>
        </div>
        <div class="field">
          <uui-label>Status</uui-label>
          <uui-select
            .options=${statusOptions}
            @change=${(e: Event) => {
              this.statusFilter = parseInt((e.target as HTMLSelectElement).value);
              this.skip = 0;
              this.load();
            }}
          ></uui-select>
        </div>
        <div class="field">
          <uui-label>Search</uui-label>
          <uui-input
            type="search"
            placeholder="Path contains…"
            .value=${this.search}
            @input=${(e: Event) => (this.search = (e.target as HTMLInputElement).value)}
            @change=${() => { this.skip = 0; this.load(); }}
          ></uui-input>
        </div>
        <div class="field">
          <uui-label>Sort</uui-label>
          <uui-select
            .options=${sortOptions}
            @change=${(e: Event) => {
              this.sort = parseInt((e.target as HTMLSelectElement).value);
              this.load();
            }}
          ></uui-select>
        </div>
        <div class="spacer"></div>
        <uui-button
          look="secondary"
          label=${this.loading ? "Refreshing…" : "Refresh"}
          ?disabled=${this.loading}
          @click=${() => this.load()}
        ></uui-button>
        ${this.selectedIds.size > 0
          ? html`
              <uui-button
                look="primary"
                color="danger"
                label=${`Delete selected (${this.selectedIds.size})`}
                @click=${this.deleteSelected}
              ></uui-button>
            `
          : nothing}
      </div>

      ${this.error ? html`<div class="error-banner">${this.error}</div>` : nothing}
      ${this.loading ? html`<uui-loader></uui-loader>` : nothing}

      <uui-table>
        <uui-table-head>
          <uui-table-head-cell class="checkbox-cell">
            <input
              type="checkbox"
              aria-label="Select all"
              .checked=${allSelected}
              @change=${this.toggleSelectAll}
            >
          </uui-table-head-cell>
          <uui-table-head-cell>Path</uui-table-head-cell>
          <uui-table-head-cell>Hostname</uui-table-head-cell>
          <uui-table-head-cell>Hits</uui-table-head-cell>
          <uui-table-head-cell>Query strings</uui-table-head-cell>
          <uui-table-head-cell>Status</uui-table-head-cell>
          <uui-table-head-cell></uui-table-head-cell>
        </uui-table-head>
        ${this.items.length === 0 && !this.loading
          ? html`
              <uui-table-row>
                <uui-table-cell colspan="7">
                  <div class="empty">No hits.</div>
                </uui-table-cell>
              </uui-table-row>
            `
          : this.items.map(
              (item) => html`
                <uui-table-row>
                  <uui-table-cell class="checkbox-cell">
                    <input
                      type="checkbox"
                      aria-label=${`Select ${item.path}`}
                      .checked=${this.selectedIds.has(item.id)}
                      @change=${() => this.toggleSelect(item.id)}
                    >
                  </uui-table-cell>
                  <uui-table-cell>
                    <a
                      class="path-link"
                      href=${`https://${item.hostname}${item.path}`}
                      target="_blank"
                      rel="noopener noreferrer"
                    >${item.path}</a>
                  </uui-table-cell>
                  <uui-table-cell>${item.hostname}</uui-table-cell>
                  <uui-table-cell>${item.hitCount}</uui-table-cell>
                  <uui-table-cell>${item.queryStringCount}</uui-table-cell>
                  <uui-table-cell class="status-cell">
                    <uui-tag look="${this.statusColor(item.status) === "default" ? "default" : "primary"}"
                             color="${this.statusColor(item.status)}">
                      ${STATUS_LABELS[item.status]}
                    </uui-tag>
                  </uui-table-cell>
                  <uui-table-cell>
                    <div class="row-actions">
                      <uui-button
                        compact
                        look="secondary"
                        label="Details"
                        @click=${() => this.openDetails(item)}
                      ></uui-button>
                      <uui-button
                        compact
                        look="secondary"
                        label="Redirect"
                        @click=${() => this.openRedirectPicker(item)}
                      ></uui-button>
                      <uui-button
                        compact
                        look="secondary"
                        label="Ignore"
                        @click=${() => this.openIgnoreModal(item)}
                      ></uui-button>
                      <uui-button
                        compact
                        look="secondary"
                        color="danger"
                        label="Delete"
                        @click=${() => this.deleteOne(item)}
                      ></uui-button>
                    </div>
                  </uui-table-cell>
                </uui-table-row>
              `,
            )}
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
}

declare global {
  interface HTMLElementTagNameMap {
    "not-found-tracker-hits-tab": HitsTabElement;
  }
}

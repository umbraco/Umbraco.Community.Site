import { LitElement, html, css, nothing } from "lit";
import { customElement, state } from "lit/decorators.js";
import { NotFoundTrackerApi } from "../api/not-found-tracker-api.js";
import type { HitListItem, HitListResponse } from "../api/types.js";
import "./modals/create-redirect-modal.element.js";
import "./modals/add-ignore-rule-modal.element.js";

const STATUS_LABELS = ["Active", "Ignored", "Redirected"];
const SORTS = [
  { value: 0, label: "Recently seen" },
  { value: 1, label: "Popularity" },
  { value: 2, label: "First seen" },
];

@customElement("not-found-tracker-hits-tab")
export class HitsTabElement extends LitElement {
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
  @state() private redirectingFor: HitListItem | null = null;
  @state() private ignoringFor: HitListItem | null = null;

  static styles = css`
    :host { display: block; }
    .toolbar {
      display: flex; gap: var(--uui-size-space-3, 12px); align-items: end;
      flex-wrap: wrap; margin-bottom: var(--uui-size-space-4, 16px);
    }
    .field { display: flex; flex-direction: column; gap: 4px; }
    .field label { font-size: 12px; color: var(--uui-color-text-alt, #686c87); }
    select, input { padding: 6px 8px; border: 1px solid var(--uui-color-border, #d6d6d6); border-radius: 3px; }
    table { width: 100%; border-collapse: collapse; }
    th, td { text-align: left; padding: 8px; border-bottom: 1px solid var(--uui-color-divider, #e9e9eb); }
    th { background: var(--uui-color-surface-alt, #f9f9fb); }
    .badge { padding: 2px 8px; border-radius: 10px; font-size: 11px; background: var(--uui-color-surface-alt, #f0f0f4); }
    .badge.status-2 { background: #d6e9d6; color: #2d6a2d; }
    .badge.status-1 { background: #e8e1ee; color: #6a4c8c; }
    .pagination { display: flex; gap: 8px; align-items: center; margin-top: 16px; }
    .pagination button[disabled] { opacity: .5; }
    .row-checkbox { width: 32px; }
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

  private async deleteSelected() {
    if (this.selectedIds.size === 0) return;
    const ok = confirm(`Delete ${this.selectedIds.size} hit(s)?`);
    if (!ok) return;
    await NotFoundTrackerApi.bulkDeleteHits([...this.selectedIds]);
    this.selectedIds.clear();
    await this.load();
  }

  private async deleteOne(id: number) {
    if (!confirm("Delete this hit?")) return;
    await NotFoundTrackerApi.deleteHit(id);
    await this.load();
  }

  render() {
    return html`
      <div class="toolbar">
        <div class="field">
          <label>Hostname</label>
          <select @change=${(e: Event) => { this.hostnameFilter = (e.target as HTMLSelectElement).value; this.skip = 0; this.load(); }}>
            <option value="">All sites</option>
            ${this.hostnames.map(h => html`<option value=${h} ?selected=${this.hostnameFilter === h}>${h}</option>`)}
          </select>
        </div>
        <div class="field">
          <label>Status</label>
          <select @change=${(e: Event) => { this.statusFilter = parseInt((e.target as HTMLSelectElement).value); this.skip = 0; this.load(); }}>
            <option value="0" ?selected=${this.statusFilter === 0}>Active</option>
            <option value="1" ?selected=${this.statusFilter === 1}>Ignored</option>
            <option value="2" ?selected=${this.statusFilter === 2}>Redirected</option>
          </select>
        </div>
        <div class="field">
          <label>Search</label>
          <input type="search" placeholder="Path contains..." @input=${(e: Event) => { this.search = (e.target as HTMLInputElement).value; this.skip = 0; }} @change=${() => this.load()}>
        </div>
        <div class="field">
          <label>Sort</label>
          <select @change=${(e: Event) => { this.sort = parseInt((e.target as HTMLSelectElement).value); this.load(); }}>
            ${SORTS.map(s => html`<option value=${s.value} ?selected=${this.sort === s.value}>${s.label}</option>`)}
          </select>
        </div>
        ${this.selectedIds.size > 0
          ? html`<button @click=${this.deleteSelected}>Delete selected (${this.selectedIds.size})</button>`
          : nothing}
      </div>

      ${this.error ? html`<div style="color: red;">Error: ${this.error}</div>` : nothing}
      ${this.loading ? html`<div>Loading…</div>` : nothing}

      <table>
        <thead>
          <tr>
            <th class="row-checkbox"></th>
            <th>Path</th>
            <th>Hostname</th>
            <th>Hits</th>
            <th>Last seen</th>
            <th>Status</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          ${this.items.length === 0 && !this.loading
            ? html`<tr><td colspan="7" style="text-align:center;padding:24px;color:#888;">No hits.</td></tr>`
            : this.items.map(item => html`
              <tr>
                <td class="row-checkbox">
                  <input type="checkbox" .checked=${this.selectedIds.has(item.id)} @change=${() => this.toggleSelect(item.id)}>
                </td>
                <td>${item.path}</td>
                <td>${item.hostname}</td>
                <td>${item.hitCount}</td>
                <td>${new Date(item.lastSeenUtc).toLocaleString()}</td>
                <td><span class="badge status-${item.status}">${STATUS_LABELS[item.status]}</span></td>
                <td>
                  <button @click=${() => (this.redirectingFor = item)}>Redirect</button>
                  <button @click=${() => (this.ignoringFor = item)}>Ignore</button>
                  <button @click=${() => this.deleteOne(item.id)}>Delete</button>
                </td>
              </tr>
            `)}
        </tbody>
      </table>

      <div class="pagination">
        <button @click=${() => { this.skip = Math.max(0, this.skip - this.take); this.load(); }} ?disabled=${this.skip === 0}>‹ Prev</button>
        <span>${this.skip + 1}-${Math.min(this.skip + this.take, this.total)} of ${this.total}</span>
        <button @click=${() => { this.skip += this.take; this.load(); }} ?disabled=${this.skip + this.take >= this.total}>Next ›</button>
      </div>

      ${this.redirectingFor
        ? html`
          <div style="position:fixed;inset:0;background:rgba(0,0,0,.4);display:flex;align-items:center;justify-content:center;z-index:1000;">
            <div style="background:white;border-radius:6px;">
              <not-found-tracker-create-redirect-modal
                .hitId=${this.redirectingFor.id}
                .hitPath=${this.redirectingFor.path}
                @done=${() => { this.redirectingFor = null; this.load(); }}
                @cancel=${() => (this.redirectingFor = null)}
              ></not-found-tracker-create-redirect-modal>
            </div>
          </div>
        `
        : nothing}
      ${this.ignoringFor
        ? html`
          <div style="position:fixed;inset:0;background:rgba(0,0,0,.4);display:flex;align-items:center;justify-content:center;z-index:1000;">
            <div style="background:white;border-radius:6px;">
              <not-found-tracker-add-ignore-rule-modal
                .hitId=${this.ignoringFor.id}
                .suggestedPath=${this.ignoringFor.path}
                .suggestedHostname=${this.ignoringFor.hostname}
                @done=${() => { this.ignoringFor = null; this.load(); }}
                @cancel=${() => (this.ignoringFor = null)}
              ></not-found-tracker-add-ignore-rule-modal>
            </div>
          </div>
        `
        : nothing}
    `;
  }
}

declare global {
  interface HTMLElementTagNameMap {
    "not-found-tracker-hits-tab": HitsTabElement;
  }
}

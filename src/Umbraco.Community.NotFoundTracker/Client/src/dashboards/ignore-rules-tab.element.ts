import { LitElement, html, css, nothing } from "lit";
import { customElement, state } from "lit/decorators.js";
import { NotFoundTrackerApi } from "../api/not-found-tracker-api.js";
import type { IgnoreRuleItem } from "../api/types.js";
import "./modals/add-ignore-rule-modal.element.js";

const MATCH_LABELS = ["Exact", "Prefix"];
const SOURCE_LABELS = ["User-defined", "Auto-preset", "Config"];

@customElement("not-found-tracker-ignore-rules-tab")
export class IgnoreRulesTabElement extends LitElement {
  @state() private rules: IgnoreRuleItem[] = [];
  @state() private loading = false;
  @state() private error: string | null = null;
  @state() private sourceFilter: number | "all" = "all";
  @state() private hostnameFilter = "";
  @state() private search = "";
  @state() private addingRule = false;

  static styles = css`
    :host { display: block; }
    .toolbar { display: flex; gap: 12px; align-items: end; flex-wrap: wrap; margin-bottom: 16px; }
    .field { display: flex; flex-direction: column; gap: 4px; }
    .field label { font-size: 12px; color: var(--uui-color-text-alt, #686c87); }
    select, input { padding: 6px 8px; border: 1px solid var(--uui-color-border, #d6d6d6); border-radius: 3px; }
    table { width: 100%; border-collapse: collapse; }
    th, td { text-align: left; padding: 8px; border-bottom: 1px solid var(--uui-color-divider, #e9e9eb); }
    th { background: var(--uui-color-surface-alt, #f9f9fb); }
    .badge { padding: 2px 8px; border-radius: 10px; font-size: 11px; background: var(--uui-color-surface-alt, #f0f0f4); }
    .badge.source-1 { background: #e3effa; color: #1f5a8a; }
    .badge.source-2 { background: #f0e3fa; color: #5a1f8a; }
    .readonly { opacity: .7; }
    .lock-icon { font-size: 14px; margin-right: 4px; }
  `;

  connectedCallback() {
    super.connectedCallback();
    this.load();
  }

  private async load() {
    this.loading = true;
    this.error = null;
    try {
      this.rules = await NotFoundTrackerApi.listIgnoreRules();
    } catch (e) {
      this.error = (e as Error).message;
    } finally {
      this.loading = false;
    }
  }

  private filteredRules() {
    return this.rules.filter(r => {
      if (this.sourceFilter !== "all" && r.source !== this.sourceFilter) return false;
      if (this.hostnameFilter && (r.hostname ?? "") !== this.hostnameFilter) return false;
      if (this.search && !r.path.includes(this.search.toLowerCase())) return false;
      return true;
    });
  }

  private hostnames() {
    return [...new Set(this.rules.map(r => r.hostname ?? "").filter(Boolean))];
  }

  private async deleteOne(rule: IgnoreRuleItem) {
    if (!confirm(`Delete ignore rule for "${rule.path}"?`)) return;
    try {
      await NotFoundTrackerApi.deleteIgnoreRule(rule.id);
      await this.load();
    } catch (e) {
      alert(`Delete failed: ${(e as Error).message}`);
    }
  }

  private async reseed() {
    if (!confirm("Re-seed the built-in auto-preset? This inserts any missing default rules and reconciles config-seeded rules.")) return;
    try {
      await NotFoundTrackerApi.reseedAutoPreset();
      await this.load();
    } catch (e) {
      alert(`Re-seed failed: ${(e as Error).message}`);
    }
  }

  render() {
    const filtered = this.filteredRules();
    return html`
      <div class="toolbar">
        <div class="field">
          <label>Source</label>
          <select @change=${(e: Event) => { const v = (e.target as HTMLSelectElement).value; this.sourceFilter = v === "all" ? "all" : parseInt(v); }}>
            <option value="all">All</option>
            <option value="0">User-defined</option>
            <option value="1">Auto-preset</option>
            <option value="2">Config</option>
          </select>
        </div>
        <div class="field">
          <label>Hostname</label>
          <select @change=${(e: Event) => (this.hostnameFilter = (e.target as HTMLSelectElement).value)}>
            <option value="">All</option>
            ${this.hostnames().map(h => html`<option value=${h}>${h}</option>`)}
          </select>
        </div>
        <div class="field">
          <label>Search</label>
          <input type="search" placeholder="Path contains..." @input=${(e: Event) => (this.search = (e.target as HTMLInputElement).value)}>
        </div>
        <div style="margin-left:auto; display:flex; gap:8px; align-items:flex-end;">
          <button @click=${() => (this.addingRule = true)}>Add rule</button>
          <button @click=${this.reseed}>Re-seed auto-preset</button>
        </div>
      </div>

      ${this.error ? html`<div style="color:red;">${this.error}</div>` : nothing}
      ${this.loading ? html`<div>Loading…</div>` : nothing}

      <table>
        <thead>
          <tr>
            <th>Path</th>
            <th>Match</th>
            <th>Hostname</th>
            <th>Source</th>
            <th>Note</th>
            <th>Created</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          ${filtered.length === 0 && !this.loading
            ? html`<tr><td colspan="7" style="text-align:center;padding:24px;color:#888;">No rules.</td></tr>`
            : filtered.map(r => html`
              <tr class=${r.isReadOnly ? "readonly" : ""}>
                <td>${r.isReadOnly ? html`<span class="lock-icon" title="Managed via appsettings.json">🔒</span>` : ""}${r.path}</td>
                <td>${MATCH_LABELS[r.matchType]}</td>
                <td>${r.hostname ?? "All sites"}</td>
                <td><span class="badge source-${r.source}">${SOURCE_LABELS[r.source]}</span></td>
                <td>${r.note ?? ""}</td>
                <td>${new Date(r.createdUtc).toLocaleDateString()}</td>
                <td>
                  ${r.isReadOnly
                    ? nothing
                    : html`<button @click=${() => this.deleteOne(r)}>Delete</button>`}
                </td>
              </tr>
            `)}
        </tbody>
      </table>

      ${this.addingRule
        ? html`
          <div style="position:fixed;inset:0;background:rgba(0,0,0,.4);display:flex;align-items:center;justify-content:center;z-index:1000;">
            <div style="background:white;border-radius:6px;">
              <not-found-tracker-add-ignore-rule-modal
                .hitId=${0}
                .suggestedPath=${""}
                .suggestedHostname=${null}
                @done=${() => { this.addingRule = false; this.load(); }}
                @cancel=${() => (this.addingRule = false)}
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
    "not-found-tracker-ignore-rules-tab": IgnoreRulesTabElement;
  }
}

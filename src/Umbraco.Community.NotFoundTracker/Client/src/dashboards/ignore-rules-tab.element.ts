import { LitElement, html, css, nothing } from "lit";
import { customElement, state } from "lit/decorators.js";
import { UmbElementMixin } from "@umbraco-cms/backoffice/element-api";
import { UMB_MODAL_MANAGER_CONTEXT, type UmbModalManagerContext, umbConfirmModal } from "@umbraco-cms/backoffice/modal";
import { NotFoundTrackerApi } from "../api/not-found-tracker-api.js";
import type { IgnoreRuleItem } from "../api/types.js";
import { ADD_IGNORE_RULE_MODAL } from "./modals/add-ignore-rule-modal.token.js";

const MATCH_LABELS = ["Exact", "Prefix"];
const SOURCE_LABELS = ["User-defined", "Auto-preset", "Config"];

@customElement("not-found-tracker-ignore-rules-tab")
export class IgnoreRulesTabElement extends UmbElementMixin(LitElement) {
  @state() private rules: IgnoreRuleItem[] = [];
  @state() private loading = false;
  @state() private error: string | null = null;
  @state() private sourceFilter: number | "all" = "all";
  @state() private hostnameFilter = "";
  @state() private search = "";

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
    .path-cell { display: inline-flex; align-items: center; gap: var(--uui-size-space-2); }
    .readonly { opacity: 0.7; }
    .empty {
      text-align: center;
      padding: var(--uui-size-space-6);
      color: var(--uui-color-text-alt);
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
    return this.rules.filter((r) => {
      if (this.sourceFilter !== "all" && r.source !== this.sourceFilter) return false;
      if (this.hostnameFilter && (r.hostname ?? "") !== this.hostnameFilter) return false;
      if (this.search && !r.path.includes(this.search.toLowerCase())) return false;
      return true;
    });
  }

  private hostnames() {
    return [...new Set(this.rules.map((r) => r.hostname ?? "").filter(Boolean))];
  }

  private async deleteOne(rule: IgnoreRuleItem) {
    try {
      await umbConfirmModal(this, {
        headline: "Delete ignore rule?",
        content: html`
          <p>This removes the rule that ignores <strong>${rule.path}</strong>.</p>
          <p>
            Future requests to URLs matching this rule will be <strong>recorded as 404s again</strong>
            and reappear in the Hits list.
          </p>
        `,
        color: "danger",
        confirmLabel: "Delete rule",
      });
    } catch {
      return; // cancelled
    }
    try {
      await NotFoundTrackerApi.deleteIgnoreRule(rule.id);
      await this.load();
    } catch (e) {
      this.error = `Delete failed: ${(e as Error).message}`;
    }
  }

  private async openAddRuleModal() {
    if (!this._modalManager) return;
    const modal = this._modalManager.open(this, ADD_IGNORE_RULE_MODAL, {
      data: { hitId: 0, suggestedPath: "", suggestedHostname: null },
    });
    const submitted = await modal.onSubmit().then(() => true).catch(() => false);
    if (submitted) await this.load();
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

  private sourceColor(source: number): "default" | "positive" | "warning" {
    switch (source) {
      case 1: return "warning"; // Auto-preset
      case 2: return "positive"; // Config
      default: return "default"; // User-defined
    }
  }

  render() {
    const filtered = this.filteredRules();
    const sourceOptions = [
      { name: "All sources", value: "all", selected: this.sourceFilter === "all" },
      { name: "User-defined", value: "0", selected: this.sourceFilter === 0 },
      { name: "Auto-preset", value: "1", selected: this.sourceFilter === 1 },
      { name: "Config", value: "2", selected: this.sourceFilter === 2 },
    ];
    const hostnameOptions = [
      { name: "All sites", value: "", selected: this.hostnameFilter === "" },
      ...this.hostnames().map((h) => ({ name: h, value: h, selected: this.hostnameFilter === h })),
    ];

    return html`
      <div class="toolbar">
        <div class="field">
          <uui-label>Source</uui-label>
          <uui-select
            .options=${sourceOptions}
            @change=${(e: Event) => {
              const v = (e.target as HTMLSelectElement).value;
              this.sourceFilter = v === "all" ? "all" : parseInt(v);
            }}
          ></uui-select>
        </div>
        <div class="field">
          <uui-label>Hostname</uui-label>
          <uui-select
            .options=${hostnameOptions}
            @change=${(e: Event) => (this.hostnameFilter = (e.target as HTMLSelectElement).value)}
          ></uui-select>
        </div>
        <div class="field">
          <uui-label>Search</uui-label>
          <uui-input
            type="search"
            placeholder="Path contains…"
            .value=${this.search}
            @input=${(e: Event) => (this.search = (e.target as HTMLInputElement).value)}
          ></uui-input>
        </div>
        <div class="spacer"></div>
        <uui-button
          look="primary"
          label="Add rule"
          @click=${() => this.openAddRuleModal()}
        ></uui-button>
        <uui-button
          look="secondary"
          label="Re-seed auto-preset"
          @click=${this.reseed}
        ></uui-button>
      </div>

      ${this.error ? html`<div class="error-banner">${this.error}</div>` : nothing}
      ${this.loading ? html`<uui-loader></uui-loader>` : nothing}

      <uui-table>
        <uui-table-head>
          <uui-table-head-cell>Path</uui-table-head-cell>
          <uui-table-head-cell>Match</uui-table-head-cell>
          <uui-table-head-cell>Hostname</uui-table-head-cell>
          <uui-table-head-cell>Source</uui-table-head-cell>
          <uui-table-head-cell>Note</uui-table-head-cell>
          <uui-table-head-cell>Created</uui-table-head-cell>
          <uui-table-head-cell></uui-table-head-cell>
        </uui-table-head>
        ${filtered.length === 0 && !this.loading
          ? html`
              <uui-table-row>
                <uui-table-cell colspan="7">
                  <div class="empty">No rules.</div>
                </uui-table-cell>
              </uui-table-row>
            `
          : filtered.map(
              (r) => html`
                <uui-table-row class=${r.isReadOnly ? "readonly" : ""}>
                  <uui-table-cell>
                    <span class="path-cell">
                      ${r.isReadOnly
                        ? html`<uui-icon name="icon-lock" title="Managed via appsettings.json"></uui-icon>`
                        : nothing}
                      ${r.path}
                    </span>
                  </uui-table-cell>
                  <uui-table-cell>${MATCH_LABELS[r.matchType]}</uui-table-cell>
                  <uui-table-cell>${r.hostname ?? "All sites"}</uui-table-cell>
                  <uui-table-cell>
                    <uui-tag look="${this.sourceColor(r.source) === "default" ? "default" : "primary"}"
                             color="${this.sourceColor(r.source)}">
                      ${SOURCE_LABELS[r.source]}
                    </uui-tag>
                  </uui-table-cell>
                  <uui-table-cell>${r.note ?? ""}</uui-table-cell>
                  <uui-table-cell>${new Date(r.createdUtc).toLocaleDateString()}</uui-table-cell>
                  <uui-table-cell>
                    ${r.isReadOnly
                      ? nothing
                      : html`
                          <uui-button
                            compact
                            look="secondary"
                            color="danger"
                            label="Delete"
                            @click=${() => this.deleteOne(r)}
                          ></uui-button>
                        `}
                  </uui-table-cell>
                </uui-table-row>
              `,
            )}
      </uui-table>

    `;
  }
}

declare global {
  interface HTMLElementTagNameMap {
    "not-found-tracker-ignore-rules-tab": IgnoreRulesTabElement;
  }
}

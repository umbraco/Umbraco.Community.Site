/**
 * File Import Workspace View — allows administrators to preview and apply block restriction
 * rule imports from JSON files in umbraco/BlockRestrictions/.
 *
 * Three UI states:
 * 1. Initial — "Load Preview" button
 * 2. Preview loaded — categorised sections with per-row actions for orphaned rules
 * 3. Applied — results summary with counts and any errors
 */
import {
  LitElement,
  html,
  css,
  nothing,
  customElement,
  state,
} from "@umbraco-cms/backoffice/external/lit";
import { UmbElementMixin } from "@umbraco-cms/backoffice/element-api";
import { UMB_AUTH_CONTEXT } from "@umbraco-cms/backoffice/auth";
import {
  UMB_NOTIFICATION_CONTEXT,
  type UmbNotificationContext,
} from "@umbraco-cms/backoffice/notification";
import {
  setAuthConfig,
  previewFileImport,
  applyFileImport,
  deleteRule,
  exportRuleToFile,
  type FileImportPreviewResponse,
  type FileImportApplyResponse,
  type FileImportRuleChange,
  type FileImportOrphanedRule,
} from "../api/client.js";

type ViewState = "initial" | "preview" | "applied";

@customElement("block-restrictions-file-import-dashboard")
export class FileImportDashboardElement extends UmbElementMixin(LitElement) {
  @state() private _viewState: ViewState = "initial";
  @state() private _loading = false;
  @state() private _applying = false;
  @state() private _preview: FileImportPreviewResponse | null = null;
  @state() private _result: FileImportApplyResponse | null = null;
  @state() private _showUnchanged = false;
  /** Tracks orphaned rule keys that have a pending action (to disable buttons). */
  @state() private _busyOrphanKeys = new Set<string>();

  private _notificationContext?: UmbNotificationContext;

  constructor() {
    super();

    this.consumeContext(UMB_AUTH_CONTEXT, (authContext) => {
      if (!authContext) return;
      setAuthConfig({
        token: () => authContext.getLatestToken(),
        baseUrl: authContext.getOpenApiConfiguration().base,
        credentials: authContext.getOpenApiConfiguration().credentials as RequestCredentials,
      });
    });

    this.consumeContext(UMB_NOTIFICATION_CONTEXT, (ctx) => {
      this._notificationContext = ctx;
    });
  }

  private async _loadPreview() {
    this._loading = true;
    try {
      this._preview = await previewFileImport();
      this._viewState = "preview";
    } catch (e) {
      this._notificationContext?.peek("danger", {
        data: { message: `Failed to load preview: ${e}` },
      });
    } finally {
      this._loading = false;
    }
  }

  private async _applyImport() {
    this._applying = true;
    try {
      this._result = await applyFileImport();
      this._viewState = "applied";
      this._notificationContext?.peek("positive", {
        data: { message: "File import applied successfully." },
      });
    } catch (e) {
      this._notificationContext?.peek("danger", {
        data: { message: `Failed to apply import: ${e}` },
      });
    } finally {
      this._applying = false;
    }
  }

  private async _deleteOrphanedRule(rule: FileImportOrphanedRule) {
    this._busyOrphanKeys = new Set([...this._busyOrphanKeys, rule.documentTypeKey]);
    try {
      await deleteRule(rule.documentTypeKey);
      this._notificationContext?.peek("positive", {
        data: { message: `Deleted rule for "${rule.alias}".` },
      });
      // Refresh the preview to reflect the change.
      await this._loadPreview();
    } catch (e) {
      this._notificationContext?.peek("danger", {
        data: { message: `Failed to delete rule: ${e}` },
      });
    } finally {
      const next = new Set(this._busyOrphanKeys);
      next.delete(rule.documentTypeKey);
      this._busyOrphanKeys = next;
    }
  }

  private async _saveOrphanedRuleToFile(rule: FileImportOrphanedRule) {
    this._busyOrphanKeys = new Set([...this._busyOrphanKeys, rule.documentTypeKey]);
    try {
      await exportRuleToFile(rule.documentTypeKey);
      this._notificationContext?.peek("positive", {
        data: { message: `Saved "${rule.alias}.json" to disk.` },
      });
      // Refresh the preview — the rule should move from orphaned to unchanged.
      await this._loadPreview();
    } catch (e) {
      this._notificationContext?.peek("danger", {
        data: { message: `Failed to save file: ${e}` },
      });
    } finally {
      const next = new Set(this._busyOrphanKeys);
      next.delete(rule.documentTypeKey);
      this._busyOrphanKeys = next;
    }
  }

  private _reset() {
    this._viewState = "initial";
    this._preview = null;
    this._result = null;
    this._showUnchanged = false;
    this._busyOrphanKeys = new Set();
  }

  render() {
    return html`
      <uui-box headline="Block Restrictions File Import">
        <p class="description">
          Compare block restriction rules in <code>umbraco/BlockRestrictions/</code>
          JSON files against the database. Preview the differences, then apply to
          sync the database with the files.
        </p>
        ${this._viewState === "initial" ? this._renderInitial() : nothing}
        ${this._viewState === "preview" ? this._renderPreview() : nothing}
        ${this._viewState === "applied" ? this._renderApplied() : nothing}
      </uui-box>
    `;
  }

  private _renderInitial() {
    return html`
      <uui-button
        look="primary"
        label="Load Preview"
        ?disabled=${this._loading}
        @click=${this._loadPreview}
      >
        ${this._loading ? "Loading..." : "Load Preview"}
      </uui-button>
    `;
  }

  private _renderPreview() {
    const p = this._preview!;
    return html`
      ${this._renderChangeSection(
        "New Rules",
        "These rules exist in JSON files but not in the database. Applying will add them.",
        "positive",
        p.toAdd,
      )}
      ${p.toUpdate.length > 0
        ? this._renderChangeSection(
            "Updated Rules",
            "These rules exist in both files and database but differ. Applying will update the database to match the files.",
            "warning",
            p.toUpdate,
          )
        : nothing}
      ${this._renderOrphanedSection(p.toDelete)}
      ${p.unknownAliases.length > 0
        ? html`
            <div class="section">
              <h4>
                Unknown Aliases
                <uui-badge color="danger">${p.unknownAliases.length}</uui-badge>
              </h4>
              <p class="section-description">
                These JSON files reference document type aliases that don't exist in Umbraco.
                They will be skipped during import.
              </p>
              <uui-table>
                <uui-table-head>
                  <uui-table-head-cell>Alias</uui-table-head-cell>
                </uui-table-head>
                ${p.unknownAliases.map(
                  (u) => html`
                    <uui-table-row>
                      <uui-table-cell><code>${u.alias}</code></uui-table-cell>
                    </uui-table-row>
                  `,
                )}
              </uui-table>
            </div>
          `
        : nothing}
      ${p.unchanged.length > 0
        ? html`
            <div class="section">
              <h4>
                Unchanged
                <uui-badge>${p.unchanged.length}</uui-badge>
                <uui-button
                  look="secondary"
                  compact
                  label="${this._showUnchanged ? "Hide" : "Show"}"
                  @click=${() => (this._showUnchanged = !this._showUnchanged)}
                >
                  ${this._showUnchanged ? "Hide" : "Show"}
                </uui-button>
              </h4>
              ${this._showUnchanged
                ? html`
                    <uui-table>
                      <uui-table-head>
                        <uui-table-head-cell>Alias</uui-table-head-cell>
                        <uui-table-head-cell>Blocks</uui-table-head-cell>
                      </uui-table-head>
                      ${p.unchanged.map(
                        (r) => html`
                          <uui-table-row>
                            <uui-table-cell><code>${r.alias}</code></uui-table-cell>
                            <uui-table-cell>${r.fileBlocks.length} blocks</uui-table-cell>
                          </uui-table-row>
                        `,
                      )}
                    </uui-table>
                  `
                : nothing}
            </div>
          `
        : nothing}

      <div class="apply-section">
        ${p.hasChanges
          ? html`
              <div class="apply-summary">
                <strong>Applying will:</strong>
                <ul>
                  ${p.toAdd.length > 0
                    ? html`<li>Add <strong>${p.toAdd.length}</strong> new rule${p.toAdd.length !== 1 ? "s" : ""} to the database</li>`
                    : nothing}
                  ${p.toUpdate.length > 0
                    ? html`<li>Update <strong>${p.toUpdate.length}</strong> existing rule${p.toUpdate.length !== 1 ? "s" : ""} to match files</li>`
                    : nothing}
                  ${p.toDelete.length > 0
                    ? html`<li>Delete <strong>${p.toDelete.length}</strong> orphaned rule${p.toDelete.length !== 1 ? "s" : ""} from the database</li>`
                    : nothing}
                </ul>
              </div>
            `
          : html`<p class="no-changes">No changes to apply — files match the database.</p>`}
        <div class="actions">
          <uui-button
            look="primary"
            color="positive"
            label="Apply All Changes"
            ?disabled=${!p.hasChanges || this._applying}
            @click=${this._applyImport}
          >
            ${this._applying ? "Applying..." : "Apply All Changes"}
          </uui-button>
          <uui-button
            look="secondary"
            label="Cancel"
            @click=${this._reset}
          >
            Cancel
          </uui-button>
        </div>
      </div>
    `;
  }

  private _renderChangeSection(
    title: string,
    description: string,
    color: string,
    rules: FileImportRuleChange[],
  ) {
    return html`
      <div class="section">
        <h4>
          ${title}
          <uui-badge color=${color}>${rules.length}</uui-badge>
        </h4>
        <p class="section-description">${description}</p>
        ${rules.length > 0
          ? html`
              <uui-table>
                <uui-table-head>
                  <uui-table-head-cell>Alias</uui-table-head-cell>
                  <uui-table-head-cell>Blocks (file)</uui-table-head-cell>
                  <uui-table-head-cell>Added</uui-table-head-cell>
                  <uui-table-head-cell>Removed</uui-table-head-cell>
                </uui-table-head>
                ${rules.map(
                  (r) => html`
                    <uui-table-row>
                      <uui-table-cell><code>${r.alias}</code></uui-table-cell>
                      <uui-table-cell>${r.fileBlocks.length} blocks</uui-table-cell>
                      <uui-table-cell>
                        ${r.blocksAdded.length > 0
                          ? r.blocksAdded.map(
                              (a) => html`<uui-tag color="positive" look="secondary">${a}</uui-tag> `,
                            )
                          : html`<span class="muted">&mdash;</span>`}
                      </uui-table-cell>
                      <uui-table-cell>
                        ${r.blocksRemoved.length > 0
                          ? r.blocksRemoved.map(
                              (a) => html`<uui-tag color="danger" look="secondary">${a}</uui-tag> `,
                            )
                          : html`<span class="muted">&mdash;</span>`}
                      </uui-table-cell>
                    </uui-table-row>
                  `,
                )}
              </uui-table>
            `
          : html`<p class="empty-hint">No new rules to import.</p>`}
      </div>
    `;
  }

  private _renderOrphanedSection(rules: FileImportOrphanedRule[]) {
    return html`
      <div class="section">
        <h4>
          Orphaned Rules
          <uui-badge color=${rules.length > 0 ? "danger" : "default"}>${rules.length}</uui-badge>
        </h4>
        <p class="section-description">
          These database rules have no corresponding JSON file. Applying will delete
          them from the database unless you save them to disk first.
        </p>
        ${rules.length > 0
          ? html`
              <uui-table>
                <uui-table-head>
                  <uui-table-head-cell>Alias</uui-table-head-cell>
                  <uui-table-head-cell>Current Blocks</uui-table-head-cell>
                  <uui-table-head-cell>Actions</uui-table-head-cell>
                </uui-table-head>
                ${rules.map((r) => {
                  const busy = this._busyOrphanKeys.has(r.documentTypeKey);
                  return html`
                    <uui-table-row>
                      <uui-table-cell><code>${r.alias}</code></uui-table-cell>
                      <uui-table-cell>${r.currentBlocks.length} blocks</uui-table-cell>
                      <uui-table-cell class="actions-cell">
                        <uui-button
                          look="secondary"
                          compact
                          label="Save to File"
                          ?disabled=${busy}
                          @click=${() => this._saveOrphanedRuleToFile(r)}
                        >
                          Save to File
                        </uui-button>
                        <uui-button
                          look="secondary"
                          color="danger"
                          compact
                          label="Delete Rule"
                          ?disabled=${busy}
                          @click=${() => this._deleteOrphanedRule(r)}
                        >
                          Delete Rule
                        </uui-button>
                      </uui-table-cell>
                    </uui-table-row>
                  `;
                })}
              </uui-table>
            `
          : html`<p class="empty-hint">No orphaned rules.</p>`}
      </div>
    `;
  }

  private _renderApplied() {
    const r = this._result!;
    return html`
      <div class="results">
        <h4>Import Complete</h4>
        <div class="counts">
          ${r.added > 0 ? html`<uui-tag color="positive">Added: ${r.added}</uui-tag>` : nothing}
          ${r.updated > 0 ? html`<uui-tag color="warning">Updated: ${r.updated}</uui-tag>` : nothing}
          ${r.deleted > 0 ? html`<uui-tag color="danger">Deleted: ${r.deleted}</uui-tag>` : nothing}
          ${r.skipped > 0 ? html`<uui-tag>Skipped: ${r.skipped}</uui-tag>` : nothing}
        </div>
        ${r.errors.length > 0
          ? html`
              <div class="errors">
                <h5>Errors</h5>
                ${r.errors.map(
                  (e) => html`
                    <p class="error-item">
                      <strong>${e.alias}:</strong> ${e.error}
                    </p>
                  `,
                )}
              </div>
            `
          : nothing}
        <uui-button
          look="secondary"
          label="Start Over"
          @click=${this._reset}
        >
          Start Over
        </uui-button>
      </div>
    `;
  }

  static styles = css`
    :host {
      display: block;
      padding: var(--uui-size-layout-1);
    }

    .description {
      margin-bottom: var(--uui-size-space-4);
      color: var(--uui-color-text-alt);
    }

    code {
      background: var(--uui-color-surface-alt);
      padding: 2px 6px;
      border-radius: var(--uui-border-radius);
      font-size: 0.9em;
    }

    .section {
      margin: var(--uui-size-space-5) 0;
    }

    .section h4 {
      display: flex;
      align-items: center;
      gap: var(--uui-size-space-2);
      margin-bottom: var(--uui-size-space-3);
    }

    .section-description {
      color: var(--uui-color-text-alt);
      margin-bottom: var(--uui-size-space-3);
      font-size: 0.9em;
    }

    .empty-hint {
      color: var(--uui-color-text-alt);
      font-style: italic;
      margin: 0;
    }

    .muted {
      color: var(--uui-color-text-alt);
    }

    .no-changes {
      color: var(--uui-color-text-alt);
      font-style: italic;
    }

    .apply-section {
      margin-top: var(--uui-size-space-6);
      padding-top: var(--uui-size-space-4);
      border-top: 1px solid var(--uui-color-border);
    }

    .apply-summary {
      margin-bottom: var(--uui-size-space-4);
    }

    .apply-summary ul {
      margin: var(--uui-size-space-2) 0 0 var(--uui-size-space-4);
      padding: 0;
    }

    .apply-summary li {
      margin-bottom: var(--uui-size-space-1);
    }

    .actions {
      display: flex;
      gap: var(--uui-size-space-3);
    }

    .actions-cell {
      display: flex;
      gap: var(--uui-size-space-2);
    }

    .results h4 {
      margin-bottom: var(--uui-size-space-3);
    }

    .counts {
      display: flex;
      gap: var(--uui-size-space-2);
      margin-bottom: var(--uui-size-space-4);
      flex-wrap: wrap;
    }

    .errors {
      margin-bottom: var(--uui-size-space-4);
      padding: var(--uui-size-space-3);
      background: var(--uui-color-danger-standalone);
      border-radius: var(--uui-border-radius);
    }

    .error-item {
      margin: var(--uui-size-space-1) 0;
    }

    uui-tag {
      margin-right: var(--uui-size-space-1);
      margin-bottom: var(--uui-size-space-1);
    }

    uui-table {
      width: 100%;
    }
  `;
}

export default FileImportDashboardElement;

declare global {
  interface HTMLElementTagNameMap {
    "block-restrictions-file-import-dashboard": FileImportDashboardElement;
  }
}

import {
  LitElement,
  html,
  css,
  nothing,
  customElement,
  state,
} from "@umbraco-cms/backoffice/external/lit";
import { UmbElementMixin } from "@umbraco-cms/backoffice/element-api";
import { UMB_DOCUMENT_TYPE_WORKSPACE_CONTEXT } from "@umbraco-cms/backoffice/document-type";
import { UMB_AUTH_CONTEXT } from "@umbraco-cms/backoffice/auth";
import {
  UMB_NOTIFICATION_CONTEXT,
  type UmbNotificationContext,
} from "@umbraco-cms/backoffice/notification";
import {
  setAuthConfig,
  getRule,
  saveRule,
  deleteRule,
  getElementTypes,
  getBlockDataTypes,
  type ElementTypeInfo,
  type BlockDataTypeInfo,
} from "../../api/client.js";

@customElement("block-restrictions-workspace-view")
export default class BlockRestrictionsElement extends UmbElementMixin(LitElement) {
  @state() private _docTypeKey: string | undefined;
  @state() private _loading = true;
  @state() private _saving = false;
  @state() private _restrictionsEnabled = false;
  @state() private _elementTypes: ElementTypeInfo[] = [];
  @state() private _selectedAliases: Set<string> = new Set();
  @state() private _filterText = "";
  @state() private _showSettings = false;
  @state() private _showCompositions = false;
  @state() private _blockDataTypes: BlockDataTypeInfo[] = [];
  @state() private _selectedMasterKey: string | undefined;

  private _notificationContext?: UmbNotificationContext;
  private _authReady = false;

  constructor() {
    super();

    this.consumeContext(UMB_NOTIFICATION_CONTEXT, (context) => {
      this._notificationContext = context;
    });

    this.consumeContext(UMB_AUTH_CONTEXT, (authContext) => {
      const config = authContext?.getOpenApiConfiguration();
      setAuthConfig({
        token: config?.token ?? undefined,
        baseUrl: config?.base ?? "",
        credentials: config?.credentials ?? "same-origin",
      });
      this._authReady = true;
      this._tryLoad();
    });

    this.consumeContext(UMB_DOCUMENT_TYPE_WORKSPACE_CONTEXT, (context) => {
      if (!context) return;
      this.observe(context.unique, (unique) => {
        if (unique) {
          this._docTypeKey = unique;
          this._tryLoad();
        }
      });
    });
  }

  private _tryLoad() {
    if (this._authReady && this._docTypeKey) {
      this._loadData();
    }
  }

  private async _loadData() {
    if (!this._docTypeKey) return;
    this._loading = true;

    try {
      const [rule, elementTypes, blockDataTypes] = await Promise.all([
        getRule(this._docTypeKey),
        getElementTypes(),
        getBlockDataTypes(),
      ]);

      this._elementTypes = elementTypes;
      this._blockDataTypes = blockDataTypes;

      if (rule) {
        this._restrictionsEnabled = true;
        this._selectedAliases = new Set(rule.allowedBlockAliases);
      } else {
        this._restrictionsEnabled = false;
        this._selectedAliases = new Set();
      }
    } catch (error) {
      console.error("Failed to load block restriction data:", error);
      this._notificationContext?.peek("danger", {
        data: { message: "Failed to load block restriction data." },
      });
    } finally {
      this._loading = false;
    }
  }

  private _onToggleChange(e: Event) {
    const input = e.target as HTMLInputElement;
    this._restrictionsEnabled = input.checked;
  }

  private _onCheckboxChange(alias: string, checked: boolean) {
    const updated = new Set(this._selectedAliases);
    if (checked) {
      updated.add(alias);
    } else {
      updated.delete(alias);
    }
    this._selectedAliases = updated;
  }

  private _onSelectAll() {
    this._selectedAliases = new Set(
      this._filteredElementTypes.map((et) => et.alias)
    );
  }

  private _onDeselectAll() {
    const filteredAliases = new Set(
      this._filteredElementTypes.map((et) => et.alias)
    );
    const updated = new Set(this._selectedAliases);
    for (const alias of filteredAliases) {
      updated.delete(alias);
    }
    this._selectedAliases = updated;
  }

  private _onFilterInput(e: Event) {
    const input = e.target as HTMLInputElement;
    this._filterText = input.value;
  }

  private get _baseFilteredElementTypes(): ElementTypeInfo[] {
    let allowedKeys: Set<string> | undefined;
    if (this._selectedMasterKey) {
      const master = this._blockDataTypes.find(
        (dt) => dt.key === this._selectedMasterKey
      );
      if (master) {
        allowedKeys = new Set(master.contentElementTypeKeys);
      }
    }

    return this._elementTypes.filter((et) => {
      if (allowedKeys && !allowedKeys.has(et.key)) {
        return false;
      }
      if (this._filterText) {
        const lower = this._filterText.toLowerCase();
        return (
          et.name.toLowerCase().includes(lower) ||
          et.alias.toLowerCase().includes(lower)
        );
      }
      return true;
    });
  }

  private get _filteredElementTypes(): ElementTypeInfo[] {
    return this._baseFilteredElementTypes.filter((et) => {
      const lowerAlias = et.alias.toLowerCase();
      if (!this._showSettings && lowerAlias.startsWith("settings")) {
        return false;
      }
      if (!this._showCompositions && lowerAlias.startsWith("composition")) {
        return false;
      }
      return true;
    });
  }

  private get _settingsCount(): number {
    return this._baseFilteredElementTypes.filter((et) =>
      et.alias.toLowerCase().startsWith("settings")
    ).length;
  }

  private _onShowSettingsChange(e: Event) {
    const input = e.target as HTMLInputElement;
    this._showSettings = input.checked;
  }

  private get _compositionsCount(): number {
    return this._baseFilteredElementTypes.filter((et) =>
      et.alias.toLowerCase().startsWith("composition")
    ).length;
  }

  private _onShowCompositionsChange(e: Event) {
    const input = e.target as HTMLInputElement;
    this._showCompositions = input.checked;
  }

  private _onMasterDataTypeChange(e: Event) {
    const select = e.target as HTMLSelectElement;
    this._selectedMasterKey = select.value || undefined;
  }

  private async _onSave() {
    if (!this._docTypeKey) return;
    this._saving = true;

    try {
      if (this._restrictionsEnabled) {
        await saveRule(this._docTypeKey, [...this._selectedAliases]);
        this._notificationContext?.peek("positive", {
          data: { message: "Block restrictions saved." },
        });
      } else {
        await deleteRule(this._docTypeKey);
        this._selectedAliases = new Set();
        this._notificationContext?.peek("positive", {
          data: { message: "Block restrictions removed. This document type will inherit from ancestors." },
        });
      }
    } catch (error) {
      console.error("Failed to save block restrictions:", error);
      this._notificationContext?.peek("danger", {
        data: { message: "Failed to save block restrictions." },
      });
    } finally {
      this._saving = false;
    }
  }

  render() {
    if (this._loading) {
      return html`<div class="loading">
        <uui-loader></uui-loader>
      </div>`;
    }

    return html`
      <uui-box headline="Blocks">
        <div class="toggle-section">
          <uui-toggle
            label="Restrict available blocks"
            ?checked=${this._restrictionsEnabled}
            @change=${this._onToggleChange}
          ></uui-toggle>
          <p class="description">
            ${this._restrictionsEnabled
              ? `Configure which block types are available when editing content using this document type.`
              : `This document type inherits block restrictions from ancestor content nodes. Toggle on to configure specific blocks for this document type.`}
          </p>
        </div>

        ${this._restrictionsEnabled ? this._renderBlockList() : nothing}

        <div class="actions">
          <uui-button
            look="primary"
            color="positive"
            label="Update"
            ?disabled=${this._saving}
            @click=${this._onSave}
          >
            ${this._saving ? "Updating..." : "Update"}
          </uui-button>
        </div>
      </uui-box>
    `;
  }

  private _renderBlockList() {
    const filtered = this._filteredElementTypes;
    const selectedCount = this._selectedAliases.size;

    const settingsCount = this._settingsCount;
    const compositionsCount = this._compositionsCount;

    return html`
      <div class="block-list-section">
        ${this._blockDataTypes.length > 0
          ? html`<div class="data-type-filter">
              <label for="master-data-type">Filter by data type</label>
              <uui-select
                label="Filter by data type"
                id="master-data-type"
                .options=${[
                  { name: "All element types", value: "", selected: !this._selectedMasterKey },
                  ...this._blockDataTypes.map((dt) => ({
                    name: `${dt.name}`,
                    value: dt.key,
                    selected: this._selectedMasterKey === dt.key,
                  })),
                ]}
                @change=${this._onMasterDataTypeChange}
              ></uui-select>
            </div>`
          : nothing}
        <div class="block-list-header">
          <span class="count">${selectedCount} of ${filtered.length} block types selected</span>
          <div class="filter-bar">
            <uui-input
              label="Filter block types"
              type="search"
              placeholder="Filter block types..."
              .value=${this._filterText}
              @input=${this._onFilterInput}
            ></uui-input>
            <uui-button
              look="secondary"
              label="Select all"
              compact
              @click=${this._onSelectAll}
            >Select all</uui-button>
            <uui-button
              look="secondary"
              label="Deselect all"
              compact
              @click=${this._onDeselectAll}
            >Deselect all</uui-button>
          </div>
        </div>
        <div class="block-list">
          ${filtered.map(
            (et) => html`
              <uui-checkbox
                class="block-item"
                label=${et.name}
                ?checked=${this._selectedAliases.has(et.alias)}
                @change=${(e: Event) =>
                  this._onCheckboxChange(
                    et.alias,
                    (e.target as HTMLInputElement).checked
                  )}
              >
                <span class="block-item-content">
                  <umb-icon name=${et.icon}></umb-icon>
                  <span class="block-name">${et.name}</span>
                  <span class="block-alias">${et.alias}</span>
                </span>
              </uui-checkbox>
            `
          )}
          ${filtered.length === 0
            ? html`<p class="no-results">No block types match your filter.</p>`
            : nothing}
        </div>
        ${settingsCount > 0 || compositionsCount > 0
          ? html`<div class="visibility-toggles">
              ${settingsCount > 0
                ? html`<span class="toggle-with-tooltip" title="Show element types with aliases prefixed &quot;settings&quot;">
                    <uui-toggle
                      label="Show settings types (${settingsCount})"
                      ?checked=${this._showSettings}
                      @change=${this._onShowSettingsChange}
                      compact
                    ></uui-toggle>
                  </span>`
                : nothing}
              ${compositionsCount > 0
                ? html`<span class="toggle-with-tooltip" title="Show element types with aliases prefixed &quot;composition&quot;">
                    <uui-toggle
                      label="Show composition types (${compositionsCount})"
                      ?checked=${this._showCompositions}
                      @change=${this._onShowCompositionsChange}
                      compact
                    ></uui-toggle>
                  </span>`
                : nothing}
            </div>`
          : nothing}
      </div>
    `;
  }

  static styles = css`
    :host {
      display: block;
      padding: var(--uui-size-layout-1);
    }

    .loading {
      display: flex;
      justify-content: center;
      padding: var(--uui-size-layout-2);
    }

    .toggle-section {
      margin-bottom: var(--uui-size-space-5);
    }

    .description {
      color: var(--uui-color-text-alt);
      font-size: 0.9em;
      margin-top: var(--uui-size-space-2);
    }

    .block-list-section {
      margin-bottom: var(--uui-size-space-5);
    }

    .data-type-filter {
      display: flex;
      align-items: center;
      gap: var(--uui-size-space-3);
      margin-bottom: var(--uui-size-space-4);
    }

    .data-type-filter label {
      font-weight: 600;
      font-size: 0.9em;
      white-space: nowrap;
    }

    .data-type-filter uui-select {
      flex: 1;
      max-width: 350px;
    }

    .block-list-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: var(--uui-size-space-3);
      flex-wrap: wrap;
      gap: var(--uui-size-space-2);
    }

    .count {
      font-weight: 600;
      font-size: 0.9em;
    }

    .filter-bar {
      display: flex;
      align-items: center;
      gap: var(--uui-size-space-2);
    }

    .filter-bar uui-input {
      width: 200px;
    }

    .block-list {
      display: flex;
      flex-direction: column;
      border: 1px solid var(--uui-color-border);
      border-radius: var(--uui-border-radius);
      max-height: 500px;
      overflow-y: auto;
    }

    .block-item {
      display: block;
      padding: var(--uui-size-space-3) var(--uui-size-space-4);
      border-bottom: 1px solid var(--uui-color-border);
      cursor: pointer;
    }

    .block-item:last-child {
      border-bottom: none;
    }

    .block-item:hover {
      background-color: var(--uui-color-surface-emphasis);
    }

    .block-item-content {
      display: inline-flex;
      align-items: center;
      vertical-align: middle;
      gap: var(--uui-size-space-3);
      width: 100%;
    }

    .block-item-content umb-icon {
      font-size: 1.2em;
      line-height: 1;
    }

    .block-name {
      flex: 1;
    }

    .block-alias {
      color: var(--uui-color-text-alt);
      font-size: 0.85em;
      font-family: monospace;
    }

    .no-results {
      padding: var(--uui-size-space-4);
      text-align: center;
      color: var(--uui-color-text-alt);
    }

    .visibility-toggles {
      display: flex;
      justify-content: flex-end;
      gap: var(--uui-size-space-4);
      margin-top: var(--uui-size-space-3);
    }

    .toggle-with-tooltip {
      cursor: help;
    }

    .actions {
      margin-top: var(--uui-size-space-4);
    }

    umb-icon {
      font-size: 1.2em;
    }
  `;
}

declare global {
  interface HTMLElementTagNameMap {
    "block-restrictions-workspace-view": BlockRestrictionsElement;
  }
}

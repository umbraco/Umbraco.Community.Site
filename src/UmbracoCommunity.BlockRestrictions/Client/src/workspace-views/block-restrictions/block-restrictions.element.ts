/**
 * Block Restrictions Workspace View — the "Blocks" tab on the Document Type editor.
 *
 * This web component provides the administration UI for configuring block restrictions
 * on a document type. It appears as a new tab (labelled "Blocks") in the
 * Settings > Document Types > [type] workspace, alongside the native tabs
 * (Design, Composition, etc.).
 *
 * ## Purpose
 *
 * Content architects use this view to control which block types are available to
 * content editors when they're working on pages of this document type. For example,
 * a "Blog Post" document type might only allow "Rich Text", "Image", and "Code Block"
 * blocks, while a "Landing Page" might allow the full set.
 *
 * ## How restrictions work
 *
 * - **Restrictions enabled**: The admin selects specific element types (block types)
 *   from a checklist. Only these types will be available in the content editor.
 *   The rule is saved to the database keyed by document type GUID.
 *
 * - **Restrictions disabled** (default): No rule is stored. The document type
 *   inherits restrictions from ancestor content nodes when resolving at runtime.
 *   If no ancestor has a rule either, all block types are available (fail-open).
 *
 * ## UI structure
 *
 * 1. **Enable toggle** — turns restrictions on/off for this document type
 * 2. **Data type filter** (optional) — dropdown to filter the checklist by blocks
 *    configured on a specific Block Grid or Block List data type
 * 3. **Filter bar** — text search, select all, deselect all
 * 4. **Block type checklist** — scrollable list of element types with checkboxes
 * 5. **Visibility toggles** — hide/show settings types and composition types
 *    (these are element types with conventional alias prefixes that are typically
 *    not relevant as block types)
 * 6. **Update button** — saves or deletes the rule
 *
 * ## Data flow
 *
 * On load, the view fetches three things in parallel:
 * - The existing rule for this document type (if any)
 * - All element types in Umbraco (for the checklist)
 * - All restricted block data types (for the "filter by data type" dropdown)
 *
 * On save, it either:
 * - PUTs a rule with the selected block aliases (restrictions enabled)
 * - DELETEs the rule (restrictions disabled — returns to inheritance)
 *
 * ## Filtering strategy
 *
 * The checklist supports three layers of filtering, applied in order:
 * 1. Data type filter — limits to blocks configured on a specific data type
 * 2. Text filter — further limits by name or alias substring match
 * 3. Visibility toggles — hides "settings" and "composition" prefixed types
 *
 * The data type filter and visibility toggles are UI-only conveniences — they
 * are not persisted. The text filter is also transient.
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

/**
 * The workspace view element, registered as "block-restrictions-workspace-view".
 *
 * Unlike the property editors (which use Light DOM for context propagation),
 * this element uses Shadow DOM (Lit's default). It doesn't wrap a native Umbraco
 * element, so there's no need for Light DOM context propagation. Shadow DOM gives
 * us scoped styles, which is important for the complex checklist UI.
 */
@customElement("block-restrictions-workspace-view")
export default class BlockRestrictionsElement extends UmbElementMixin(LitElement) {
  // ─── Reactive state ────────────────────────────────────────────────────────

  /** The document type's unique key (GUID), from the workspace context. */
  @state() private _docTypeKey: string | undefined;

  /** Whether data is currently being loaded from the API. Shows a spinner when true. */
  @state() private _loading = true;

  /** Whether a save operation is in progress. Disables the Update button. */
  @state() private _saving = false;

  /** Whether block restrictions are enabled for this document type. */
  @state() private _restrictionsEnabled = false;

  /** All element types in Umbraco — the full list for the checklist. */
  @state() private _elementTypes: ElementTypeInfo[] = [];

  /**
   * The set of element type aliases currently selected as "allowed".
   * Stored as aliases (not GUIDs) because that's what the API expects.
   * Using a Set makes add/delete/has operations O(1).
   */
  @state() private _selectedAliases: Set<string> = new Set();

  /** The current text filter value — filters the checklist by name or alias. */
  @state() private _filterText = "";

  /** Whether to show element types with aliases prefixed "settings". */
  @state() private _showSettings = false;

  /** Whether to show element types with aliases prefixed "composition". */
  @state() private _showCompositions = false;

  /** All restricted block data types — for the "filter by data type" dropdown. */
  @state() private _blockDataTypes: BlockDataTypeInfo[] = [];

  /**
   * The selected data type key in the "filter by data type" dropdown.
   * When set, only element types configured on this data type are shown.
   * Undefined means "show all element types" (no data type filter).
   */
  @state() private _selectedMasterKey: string | undefined;

  // ─── Private state (not reactive) ──────────────────────────────────────────

  /** Umbraco's notification context — used to show success/error toast messages. */
  private _notificationContext?: UmbNotificationContext;

  /** Whether the auth context has been consumed and the API client configured. */
  private _authReady = false;

  // ─── Constructor & context consumption ─────────────────────────────────────

  /**
   * Constructor — sets up three Umbraco context consumers:
   *
   * 1. UMB_NOTIFICATION_CONTEXT — for showing toast notifications on save/error
   * 2. UMB_AUTH_CONTEXT — for configuring the API client with the Bearer token
   * 3. UMB_DOCUMENT_TYPE_WORKSPACE_CONTEXT — for getting the document type's key
   *
   * The auth and workspace contexts both call `_tryLoad()`, which waits for both
   * to be ready before fetching data from the API.
   */
  constructor() {
    super();

    // Store the notification context for showing toasts later.
    this.consumeContext(UMB_NOTIFICATION_CONTEXT, (context) => {
      this._notificationContext = context;
    });

    // Configure the API client with the auth token from Umbraco's auth context.
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

    // Get the document type key from the workspace context.
    // Uses `observe` for reactivity in case the workspace navigates between types.
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

  // ─── Data loading ──────────────────────────────────────────────────────────

  /** Guards the API call until both auth and workspace contexts are available. */
  private _tryLoad() {
    if (this._authReady && this._docTypeKey) {
      this._loadData();
    }
  }

  /**
   * Fetches all data needed by the view in a single parallel batch.
   *
   * Three API calls are made simultaneously via Promise.all:
   * 1. getRule() — the existing restriction rule for this document type (or null)
   * 2. getElementTypes() — all element types for the checklist
   * 3. getBlockDataTypes() — restricted block data types for the filter dropdown
   *
   * If a rule exists, the toggle is set to "enabled" and the selected aliases
   * are populated. If no rule exists, the toggle is "disabled" (inheritance mode).
   */
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
        // A rule exists — populate the checklist with its allowed aliases.
        this._restrictionsEnabled = true;
        this._selectedAliases = new Set(rule.allowedBlockAliases);
      } else {
        // No rule — this document type inherits from ancestors.
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

  // ─── Event handlers ────────────────────────────────────────────────────────

  /** Handles the "Restrict available blocks" toggle change. */
  private _onToggleChange(e: Event) {
    const input = e.target as HTMLInputElement;
    this._restrictionsEnabled = input.checked;
  }

  /**
   * Handles individual block type checkbox changes.
   * Creates a new Set (immutable update) so Lit detects the state change.
   */
  private _onCheckboxChange(alias: string, checked: boolean) {
    const updated = new Set(this._selectedAliases);
    if (checked) {
      updated.add(alias);
    } else {
      updated.delete(alias);
    }
    this._selectedAliases = updated;
  }

  /**
   * Selects all currently visible (filtered) block types.
   * Only selects types that pass all three filter layers.
   */
  private _onSelectAll() {
    this._selectedAliases = new Set(
      this._filteredElementTypes.map((et) => et.alias)
    );
  }

  /**
   * Deselects all currently visible (filtered) block types.
   * Only deselects types that are currently visible — preserves selections
   * for types hidden by the current filter.
   */
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

  /** Updates the text filter from the search input. */
  private _onFilterInput(e: Event) {
    const input = e.target as HTMLInputElement;
    this._filterText = input.value;
  }

  // ─── Computed / filtered lists ─────────────────────────────────────────────

  /**
   * First filter pass: applies data type filter and text filter.
   *
   * This intermediate result is used by both `_filteredElementTypes` (which
   * adds the visibility toggle filter) and the settings/compositions count
   * getters (which need counts before the visibility filter is applied).
   */
  private get _baseFilteredElementTypes(): ElementTypeInfo[] {
    // If a master data type is selected, only include element types
    // whose key is in that data type's configured content element type keys.
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
      // Data type filter: exclude types not configured on the selected data type.
      if (allowedKeys && !allowedKeys.has(et.key)) {
        return false;
      }
      // Text filter: match against name or alias (case-insensitive).
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

  /**
   * Final filter pass: applies visibility toggle filters on top of the base filter.
   *
   * Element types with aliases starting with "settings" or "composition" are hidden
   * by default because they're typically not relevant as block types (they're
   * settings models or shared compositions). The toggles at the bottom of the
   * checklist let admins reveal them when needed.
   */
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

  /**
   * Count of settings-prefixed types in the base filtered list.
   * Used for the "(N)" label on the "Show settings types" toggle.
   * Computed from `_baseFilteredElementTypes` (not `_filteredElementTypes`)
   * so the count is visible even when settings types are hidden.
   */
  private get _settingsCount(): number {
    return this._baseFilteredElementTypes.filter((et) =>
      et.alias.toLowerCase().startsWith("settings")
    ).length;
  }

  /** Handles the "Show settings types" toggle change. */
  private _onShowSettingsChange(e: Event) {
    const input = e.target as HTMLInputElement;
    this._showSettings = input.checked;
  }

  /**
   * Count of composition-prefixed types in the base filtered list.
   * Used for the "(N)" label on the "Show composition types" toggle.
   */
  private get _compositionsCount(): number {
    return this._baseFilteredElementTypes.filter((et) =>
      et.alias.toLowerCase().startsWith("composition")
    ).length;
  }

  /** Handles the "Show composition types" toggle change. */
  private _onShowCompositionsChange(e: Event) {
    const input = e.target as HTMLInputElement;
    this._showCompositions = input.checked;
  }

  /**
   * Handles the "Filter by data type" dropdown change.
   * Sets the selected master key (or undefined for "All element types").
   */
  private _onMasterDataTypeChange(e: Event) {
    const select = e.target as HTMLSelectElement;
    this._selectedMasterKey = select.value || undefined;
  }

  // ─── Save / delete ─────────────────────────────────────────────────────────

  /**
   * Saves or deletes the restriction rule.
   *
   * - If restrictions are enabled: PUTs the selected aliases to the API.
   *   The server stores them keyed by document type GUID and invalidates
   *   the resolved restrictions cache.
   *
   * - If restrictions are disabled: DELETEs the rule. The document type
   *   returns to inheritance mode (restrictions resolved from ancestor
   *   content nodes at runtime).
   *
   * Shows a toast notification on success or failure.
   */
  private async _onSave() {
    if (!this._docTypeKey) return;
    this._saving = true;

    try {
      if (this._restrictionsEnabled) {
        // Save the rule with the currently selected aliases.
        await saveRule(this._docTypeKey, [...this._selectedAliases]);
        this._notificationContext?.peek("positive", {
          data: { message: "Block restrictions saved." },
        });
      } else {
        // Delete the rule — return to inheritance.
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

  // ─── Rendering ─────────────────────────────────────────────────────────────

  /**
   * Main render method.
   *
   * Shows a loading spinner while data is being fetched, then renders the
   * configuration UI inside a `<uui-box>` (Umbraco's standard container
   * component with a headline).
   */
  render() {
    if (this._loading) {
      return html`<div class="loading">
        <uui-loader></uui-loader>
      </div>`;
    }

    return html`
      <uui-box headline="Blocks">
        <!-- Toggle section: enable/disable restrictions for this document type -->
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

        <!-- Block type checklist (only shown when restrictions are enabled) -->
        ${this._restrictionsEnabled ? this._renderBlockList() : nothing}

        <!-- Save button -->
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

  /**
   * Renders the block type selection UI.
   *
   * This includes:
   * - Data type filter dropdown (if restricted data types exist)
   * - Selection count with live region for screen readers
   * - Filter bar with text search, select all, and deselect all buttons
   * - Scrollable checklist of element types with icons, names, and aliases
   * - Visibility toggles for settings and composition types
   */
  private _renderBlockList() {
    const filtered = this._filteredElementTypes;
    const selectedCount = this._selectedAliases.size;

    const settingsCount = this._settingsCount;
    const compositionsCount = this._compositionsCount;

    return html`
      <div class="block-list-section">
        <!-- Data type filter dropdown — only shown if restricted data types exist.
             This lets admins filter the checklist to show only blocks configured
             on a specific Block Grid or Block List data type. -->
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

        <!-- Header row: selection count and filter controls -->
        <div class="block-list-header">
          <!-- Live region: screen readers announce count changes when selections change -->
          <span class="count" role="status" aria-live="polite">${selectedCount} of ${filtered.length} block types selected</span>
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

        <!-- Scrollable checklist of element types.
             tabindex="0" makes this keyboard-focusable (WCAG 2.1.1) so keyboard
             users can scroll through the list without a mouse.
             aria-label provides context for screen readers. -->
        <div class="block-list" tabindex="0" aria-label="Block types">
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
                  <!-- Decorative icon — aria-hidden because the name is in the label -->
                  <umb-icon name=${et.icon} aria-hidden="true"></umb-icon>
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

        <!-- Visibility toggles for settings and composition types.
             These element types are hidden by default because they're typically
             settings models or shared compositions, not actual block types.
             The toggles with counts let admins reveal them when needed.
             Tooltip explains the filtering convention. -->
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

  // ─── Styles ────────────────────────────────────────────────────────────────

  /**
   * Scoped styles for the workspace view.
   *
   * Uses Umbraco's design token CSS custom properties (--uui-*) for consistent
   * spacing, colors, borders, and typography that match the backoffice theme.
   *
   * The `css` tagged template literal is processed by Lit at build time for
   * efficient style sharing across component instances via adoptedStyleSheets.
   */
  static styles = css`
    /* Host element — block display with standard Umbraco layout padding. */
    :host {
      display: block;
      padding: var(--uui-size-layout-1);
    }

    /* Centered loading spinner shown while data is being fetched. */
    .loading {
      display: flex;
      justify-content: center;
      padding: var(--uui-size-layout-2);
    }

    /* The toggle + description area at the top of the view. */
    .toggle-section {
      margin-bottom: var(--uui-size-space-5);
    }

    /* Descriptive text below the toggle — muted color, slightly smaller. */
    .description {
      color: var(--uui-color-text-alt);
      font-size: 0.9em;
      margin-top: var(--uui-size-space-2);
    }

    /* Container for the entire block selection UI. */
    .block-list-section {
      margin-bottom: var(--uui-size-space-5);
    }

    /* "Filter by data type" dropdown row — label and select side by side. */
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

    /* Header row: selection count on the left, filter controls on the right.
       Wraps on narrow viewports to avoid horizontal scrolling. */
    .block-list-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: var(--uui-size-space-3);
      flex-wrap: wrap;
      gap: var(--uui-size-space-2);
    }

    /* Bold count label, e.g. "3 of 12 block types selected". */
    .count {
      font-weight: 600;
      font-size: 0.9em;
    }

    /* Filter bar: text input + action buttons in a horizontal row. */
    .filter-bar {
      display: flex;
      align-items: center;
      gap: var(--uui-size-space-2);
    }

    .filter-bar uui-input {
      width: 200px;
    }

    /* Scrollable checklist container.
       max-height with overflow-y:auto creates the scrollable region.
       The border and border-radius match Umbraco's standard container styling. */
    .block-list {
      display: flex;
      flex-direction: column;
      border: 1px solid var(--uui-color-border);
      border-radius: var(--uui-border-radius);
      max-height: 500px;
      overflow-y: auto;
    }

    /* Individual block type row — checkbox with icon, name, and alias. */
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

    /* Layout for the content inside each checkbox: icon + name + alias in a row. */
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

    /* Block name takes remaining space (flex: 1). */
    .block-name {
      flex: 1;
    }

    /* Block alias — muted monospace text aligned to the right. */
    .block-alias {
      color: var(--uui-color-text-alt);
      font-size: 0.85em;
      font-family: monospace;
    }

    /* "No block types match your filter" message. */
    .no-results {
      padding: var(--uui-size-space-4);
      text-align: center;
      color: var(--uui-color-text-alt);
    }

    /* Visibility toggle row at the bottom of the checklist. */
    .visibility-toggles {
      display: flex;
      justify-content: flex-end;
      gap: var(--uui-size-space-4);
      margin-top: var(--uui-size-space-3);
    }

    /* Tooltip cursor for the visibility toggle wrappers. */
    .toggle-with-tooltip {
      cursor: help;
    }

    /* Save button area. */
    .actions {
      margin-top: var(--uui-size-space-4);
    }

    /* Default icon size for all umb-icon elements in this component. */
    umb-icon {
      font-size: 1.2em;
    }
  `;
}

/**
 * TypeScript global augmentation — registers the custom element tag name
 * for type-safe DOM access. The tag name must match the `elementName` in
 * the workspace view manifest (see workspace-views/manifest.ts).
 */
declare global {
  interface HTMLElementTagNameMap {
    "block-restrictions-workspace-view": BlockRestrictionsElement;
  }
}

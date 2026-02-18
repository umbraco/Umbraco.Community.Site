/**
 * Block List (Restricted) — custom property editor UI element.
 *
 * This web component wraps Umbraco's native `umb-property-editor-ui-block-list`
 * element and intercepts its configuration to enforce block restrictions. It acts
 * as a transparent proxy: the content editor sees a normal Block List, but the
 * "add content" catalogue only offers block types allowed by the restriction rules.
 *
 * ## Difference from Block Grid (Restricted)
 *
 * The Block Grid version keeps all block definitions but sets `allowAtRoot: false`
 * and `allowInAreas: false` on disallowed types. The Block List version is simpler:
 * it **filters out** disallowed block definitions entirely from the config array.
 *
 * This difference exists because:
 * - Block Grid needs the definitions present so existing disallowed blocks can
 *   render (they have grid layout, area, and column span information).
 * - Block List is a flat list — if a block type isn't in the config, existing
 *   instances still render because the block content data is stored separately
 *   from the configuration. The native Block List editor handles unknown types
 *   gracefully.
 *
 * ## Architecture
 *
 * The overall architecture is identical to block-grid-restricted.element.ts:
 * - Consumes UMB_AUTH_CONTEXT and UMB_ENTITY_CONTEXT
 * - Calls the allowed-blocks API to resolve restrictions
 * - Creates the native element imperatively in Light DOM
 * - Proxies value changes back to Umbraco's property system
 * - Recreates the inner element when restrictions load after initialisation
 *
 * See block-grid-restricted.element.ts for detailed architectural documentation.
 */
import {
  LitElement,
  html,
  nothing,
  customElement,
  state,
  property,
} from "@umbraco-cms/backoffice/external/lit";
import { UmbElementMixin } from "@umbraco-cms/backoffice/element-api";
import { UMB_AUTH_CONTEXT } from "@umbraco-cms/backoffice/auth";
import { UMB_ENTITY_CONTEXT, UMB_PARENT_ENTITY_CONTEXT } from "@umbraco-cms/backoffice/entity";
import { UMB_DOCUMENT_WORKSPACE_CONTEXT } from "@umbraco-cms/backoffice/document";
import { umbExtensionsRegistry } from "@umbraco-cms/backoffice/extension-registry";
import type {
  UmbPropertyEditorUiElement,
  UmbPropertyEditorConfigCollection,
} from "@umbraco-cms/backoffice/property-editor";
import { setAuthConfig, getAllowedBlocks, type AllowedBlocksResponse } from "../../api/client.js";

/**
 * Custom element registration for the restricted Block List property editor.
 * See block-grid-restricted.element.ts for detailed explanation of the
 * `@customElement` decorator, `UmbElementMixin`, and `UmbPropertyEditorUiElement` interface.
 */
@customElement("block-list-restricted")
export default class BlockListRestrictedElement
  extends UmbElementMixin(LitElement)
  implements UmbPropertyEditorUiElement
{
  /** The property value — the Block List's JSON data (layout + content + settings). */
  @property({ attribute: false })
  value: unknown;

  /** Restriction data from the API, or null if no restrictions apply. */
  @state() private _restrictionInfo: AllowedBlocksResponse | null = null;

  /** The unmodified config from Umbraco — preserved as the "source of truth". */
  private _originalConfig?: UmbPropertyEditorConfigCollection;

  /** The config with restrictions applied — this is what the inner element receives. */
  private _effectiveConfig?: UmbPropertyEditorConfigCollection;

  /** The content node's unique key (GUID), obtained from UMB_ENTITY_CONTEXT. */
  private _entityKey: string | undefined;

  /** The document type key — fallback for new content (see block-grid-restricted). */
  private _contentTypeKey: string | undefined;

  /** The parent node key — fallback for new content (see block-grid-restricted). */
  private _parentKey: string | undefined;

  /** Whether the auth context has been consumed and the API client configured. */
  private _authReady = false;

  /** Reference to the imperatively-created native block list element. */
  private _innerElement: HTMLElement | null = null;

  /** Whether the inner element has been created and is ready for config/value sync. */
  private _innerReady = false;

  /**
   * Config setter — called by Umbraco when the property editor configuration is available.
   * Stores the original, applies restrictions, and syncs to the inner element.
   */
  set config(value: UmbPropertyEditorConfigCollection | undefined) {
    this._originalConfig = value;
    this._applyRestrictions();
    this._syncInnerConfig();
  }

  /**
   * Config getter — returns the restricted config if available, otherwise the original.
   */
  get config(): UmbPropertyEditorConfigCollection | undefined {
    return this._effectiveConfig ?? this._originalConfig;
  }

  /**
   * Renders into Light DOM for Umbraco context propagation.
   * See block-grid-restricted.element.ts for detailed explanation.
   */
  protected override createRenderRoot() {
    return this;
  }

  /**
   * Constructor — sets up context consumers for auth and entity contexts.
   * See block-grid-restricted.element.ts for detailed explanation of the
   * dual-context consumption pattern.
   */
  constructor() {
    super();

    // Consume auth context to configure the API client with Bearer token.
    this.consumeContext(UMB_AUTH_CONTEXT, (authContext) => {
      if (!authContext) return;
      const config = authContext.getOpenApiConfiguration();
      setAuthConfig({
        token: config?.token ?? undefined,
        baseUrl: config?.base ?? "",
        credentials: config?.credentials ?? "same-origin",
      });
      this._authReady = true;
      this._tryLoad();
    });

    // Consume entity context to get the content node's unique key.
    this.consumeContext(UMB_ENTITY_CONTEXT, (context) => {
      if (!context) return;
      this.observe(context.unique, (unique) => {
        if (unique) {
          this._entityKey = unique;
          this._tryLoad();
        }
      });
    });

    // Consume document workspace context for content type key (new content fallback).
    this.consumeContext(UMB_DOCUMENT_WORKSPACE_CONTEXT, (context) => {
      if (!context) return;
      this._contentTypeKey = context.getContentTypeUnique() ?? undefined;
      // Retry if initial load returned null (new content with no fallback context).
      if (this._restrictionInfo === null && this._authReady && this._entityKey) {
        this._loadRestrictions();
      }
    });

    // Consume parent entity context for parent node key (new content fallback).
    this.consumeContext(UMB_PARENT_ENTITY_CONTEXT, (parentContext) => {
      if (!parentContext) return;
      const parent = parentContext.getParent();
      this._parentKey = parent?.unique ?? undefined;
    });
  }

  /**
   * Lifecycle: element connected to the DOM.
   *
   * Ensures the native block list element is defined before creating our wrapper.
   * See block-grid-restricted.element.ts connectedCallback for detailed explanation
   * of why we trigger loading via the extension registry.
   */
  async connectedCallback() {
    super.connectedCallback();

    const tagName = "umb-property-editor-ui-block-list";

    if (!customElements.get(tagName)) {
      // Native element not defined yet. Trigger loading of its module via the
      // extension registry.
      try {
        const manifest = umbExtensionsRegistry.getByAlias(
          "Umb.PropertyEditorUi.BlockList",
        ) as any;
        if (manifest?.element) {
          await manifest.element();
        }
      } catch {
        // Ignore — fall through to whenDefined as a fallback.
      }

      // Ensure the element is actually registered before proceeding.
      await customElements.whenDefined(tagName);
    }

    if (this.isConnected) {
      this._createInnerElement();
    }
  }

  /**
   * Lifecycle: element disconnected from the DOM.
   * Cleans up the inner element to prevent memory leaks.
   */
  disconnectedCallback() {
    super.disconnectedCallback();
    if (this._innerElement) {
      this._innerElement.remove();
      this._innerElement = null;
      this._innerReady = false;
    }
  }

  /**
   * Creates (or recreates) the native block list element imperatively.
   * Proxies value change events back to Umbraco's property system.
   * See block-grid-restricted.element.ts for detailed architectural explanation.
   */
  private _createInnerElement() {
    if (this._innerElement) {
      this._innerElement.remove();
    }

    // Create a fresh instance of the native block list editor.
    this._innerElement = document.createElement("umb-property-editor-ui-block-list");

    // Proxy value change events from the inner element to this element.
    this._innerElement.addEventListener("property-value-change", (e: Event) => {
      this.value = (e as CustomEvent).detail.value;
      this.dispatchEvent(
        new CustomEvent("property-value-change", { bubbles: true, composed: true })
      );
    });

    this._innerReady = true;

    // Apply current config and value to the new element.
    const config = this._effectiveConfig ?? this._originalConfig;
    if (config) {
      (this._innerElement as any).config = config;
    }
    (this._innerElement as any).value = this.value;

    // Trigger re-render to insert the new element into the DOM.
    this.requestUpdate();
  }

  /** Syncs the config to the inner element after config changes. */
  private _syncInnerConfig() {
    if (!this._innerElement || !this._innerReady) return;
    const config = this._effectiveConfig ?? this._originalConfig;
    (this._innerElement as any).config = config;
  }

  /** Syncs the value to the inner element when Umbraco updates it. */
  private _syncInnerValue() {
    if (!this._innerElement || !this._innerReady) return;
    (this._innerElement as any).value = this.value;
  }

  /** Guards the API call until both auth and entity contexts are ready. */
  private _tryLoad() {
    if (this._authReady && this._entityKey) {
      this._loadRestrictions();
    }
  }

  /**
   * Fetches the effective block restrictions and applies them.
   * Fails open (shows all blocks) if the API call fails.
   * Recreates the inner element if restrictions arrive after initial creation.
   */
  private async _loadRestrictions() {
    if (!this._entityKey) return;

    try {
      // Pass content type key and parent key as fallback context for new content.
      this._restrictionInfo = await getAllowedBlocks(
        this._entityKey,
        this._contentTypeKey,
        this._parentKey,
      );
    } catch (error) {
      console.error("Failed to load block restrictions, showing all blocks:", error);
      this._restrictionInfo = null;
    }

    this._applyRestrictions();

    // The block list manager caches block types from its first config.
    // If restrictions loaded after the inner element was already initialised,
    // recreate it so the manager starts fresh with the restricted config.
    if (this._innerReady && this._restrictionInfo?.hasRestrictions) {
      this._createInnerElement();
    }
  }

  /**
   * Applies restriction rules to the block configuration.
   *
   * KEY DIFFERENCE FROM BLOCK GRID: This method FILTERS OUT disallowed blocks
   * entirely, rather than setting allow flags to false. The Block List editor
   * doesn't have the `allowAtRoot` / `allowInAreas` concept — it either includes
   * a block type in the catalogue or it doesn't.
   *
   * Blocks without a `contentElementTypeKey` (shouldn't happen in practice)
   * are kept as a safety measure.
   */
  private _applyRestrictions() {
    // No config yet — nothing to restrict.
    if (!this._originalConfig) {
      this._effectiveConfig = undefined;
      return;
    }

    // No restrictions — use original config.
    if (!this._restrictionInfo?.hasRestrictions) {
      this._effectiveConfig = this._originalConfig;
      return;
    }

    // Build a Set of allowed content element type keys (lowercased for
    // case-insensitive GUID comparison).
    const allowedKeys = new Set(
      this._restrictionInfo.allowedContentElementTypeKeys.map((k) =>
        k.toLowerCase()
      )
    );

    // Iterate config entries, filtering the "blocks" array.
    const configValues: Array<{ alias: string; value: unknown }> = [];
    for (const entry of this._originalConfig) {
      if (entry.alias === "blocks") {
        const blocks = entry.value as Array<{
          contentElementTypeKey?: string;
          [key: string]: unknown;
        }>;
        // Filter: only keep blocks whose content element type is in the allowed set.
        const filteredBlocks = blocks.filter((block) => {
          if (!block.contentElementTypeKey) return true;
          return allowedKeys.has(block.contentElementTypeKey.toLowerCase());
        });
        configValues.push({ alias: entry.alias, value: filteredBlocks });
      } else {
        // Non-block config entries pass through unchanged.
        configValues.push({ alias: entry.alias, value: entry.value });
      }
    }

    // Create a new config collection instance.
    this._effectiveConfig = new (this._originalConfig.constructor as new (
      values: Array<{ alias: string; value: unknown }>
    ) => UmbPropertyEditorConfigCollection)(configValues);
  }

  /**
   * Lit lifecycle: syncs value to the inner element when Umbraco updates it.
   */
  updated(changed: Map<string, unknown>) {
    if (changed.has("value")) {
      this._syncInnerValue();
    }
  }

  /**
   * Renders the info banner (when restrictions are active) and the inner element.
   * The banner uses `role="status"` for screen reader accessibility and shows
   * whether the restriction is inherited from an ancestor document type.
   */
  render() {
    return html`
      ${this._restrictionInfo?.hasRestrictions
        ? html`<div role="status" style="display:flex;align-items:center;gap:6px;padding:6px 9px;background-color:var(--uui-color-surface-emphasis);border:1px solid var(--uui-color-border);border-radius:var(--uui-border-radius);margin-bottom:9px;font-size:0.85em;color:var(--uui-color-text-alt);">
            <umb-icon name="icon-filter" style="font-size:1em;" aria-hidden="true"></umb-icon>
            Block types are restricted
            ${this._restrictionInfo.inheritedFromAncestor
              ? html` (inherited from <strong>${this._restrictionInfo.documentTypeAlias}</strong>)`
              : nothing}
          </div>`
        : nothing}
      ${this._innerElement ?? html`<uui-loader></uui-loader>`}
    `;
  }
}

/**
 * TypeScript global augmentation — registers the custom element tag name
 * for type-safe DOM access and template type checking.
 */
declare global {
  interface HTMLElementTagNameMap {
    "block-list-restricted": BlockListRestrictedElement;
  }
}

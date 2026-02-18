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
import { UMB_ENTITY_CONTEXT } from "@umbraco-cms/backoffice/entity";
import type {
  UmbPropertyEditorUiElement,
  UmbPropertyEditorConfigCollection,
} from "@umbraco-cms/backoffice/property-editor";
import { setAuthConfig, getAllowedBlocks, type AllowedBlocksResponse } from "../../api/client.js";

@customElement("block-grid-restricted")
export default class BlockGridRestrictedElement
  extends UmbElementMixin(LitElement)
  implements UmbPropertyEditorUiElement
{
  @property({ attribute: false })
  value: unknown;

  @state() private _restrictionInfo: AllowedBlocksResponse | null = null;

  private _originalConfig?: UmbPropertyEditorConfigCollection;
  private _effectiveConfig?: UmbPropertyEditorConfigCollection;
  private _entityKey: string | undefined;
  private _authReady = false;
  private _innerElement: HTMLElement | null = null;
  private _innerReady = false;

  set config(value: UmbPropertyEditorConfigCollection | undefined) {
    this._originalConfig = value;
    this._applyRestrictions();
    this._syncInnerConfig();
  }

  get config(): UmbPropertyEditorConfigCollection | undefined {
    return this._effectiveConfig ?? this._originalConfig;
  }

  protected override createRenderRoot() {
    return this;
  }

  constructor() {
    super();

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

    this.consumeContext(UMB_ENTITY_CONTEXT, (context) => {
      if (!context) return;
      this.observe(context.unique, (unique) => {
        if (unique) {
          this._entityKey = unique;
          this._tryLoad();
        }
      });
    });
  }

  async connectedCallback() {
    super.connectedCallback();

    await customElements.whenDefined("umb-property-editor-ui-block-grid");

    this._createInnerElement();
  }

  disconnectedCallback() {
    super.disconnectedCallback();
    if (this._innerElement) {
      this._innerElement.remove();
      this._innerElement = null;
      this._innerReady = false;
    }
  }

  private _createInnerElement() {
    if (this._innerElement) {
      this._innerElement.remove();
    }

    this._innerElement = document.createElement("umb-property-editor-ui-block-grid");

    this._innerElement.addEventListener("property-value-change", (e: Event) => {
      this.value = (e as CustomEvent).detail.value;
      this.dispatchEvent(
        new CustomEvent("property-value-change", { bubbles: true, composed: true })
      );
    });

    this._innerReady = true;

    const config = this._effectiveConfig ?? this._originalConfig;
    if (config) {
      (this._innerElement as any).config = config;
    }
    (this._innerElement as any).value = this.value;

    this.requestUpdate();
  }

  private _syncInnerConfig() {
    if (!this._innerElement || !this._innerReady) return;
    const config = this._effectiveConfig ?? this._originalConfig;
    (this._innerElement as any).config = config;
  }

  private _syncInnerValue() {
    if (!this._innerElement || !this._innerReady) return;
    (this._innerElement as any).value = this.value;
  }

  private _tryLoad() {
    if (this._authReady && this._entityKey) {
      this._loadRestrictions();
    }
  }

  private async _loadRestrictions() {
    if (!this._entityKey) return;

    try {
      this._restrictionInfo = await getAllowedBlocks(this._entityKey);
    } catch (error) {
      console.error("Failed to load block restrictions, showing all blocks:", error);
      this._restrictionInfo = null;
    }

    this._applyRestrictions();

    // The block grid manager caches block types from its first config.
    // If restrictions loaded after the inner element was already initialised,
    // recreate it so the manager starts fresh with the restricted config.
    if (this._innerReady && this._restrictionInfo?.hasRestrictions) {
      this._createInnerElement();
    }
  }

  private _applyRestrictions() {
    if (!this._originalConfig) {
      this._effectiveConfig = undefined;
      return;
    }

    if (!this._restrictionInfo?.hasRestrictions) {
      this._effectiveConfig = this._originalConfig;
      return;
    }

    const allowedKeys = new Set(
      this._restrictionInfo.allowedContentElementTypeKeys.map((k) =>
        k.toLowerCase()
      )
    );

    const configValues: Array<{ alias: string; value: unknown }> = [];
    for (const entry of this._originalConfig) {
      if (entry.alias === "blocks") {
        const blocks = entry.value as Array<{
          contentElementTypeKey?: string;
          allowAtRoot?: boolean;
          allowInAreas?: boolean;
          [key: string]: unknown;
        }>;
        // Keep ALL block type definitions so existing blocks can render,
        // but disable adding for non-allowed types.
        const modifiedBlocks = blocks.map((block) => {
          if (!block.contentElementTypeKey) return block;
          if (allowedKeys.has(block.contentElementTypeKey.toLowerCase())) {
            return block;
          }
          return { ...block, allowAtRoot: false, allowInAreas: false };
        });
        configValues.push({ alias: entry.alias, value: modifiedBlocks });
      } else {
        configValues.push({ alias: entry.alias, value: entry.value });
      }
    }

    this._effectiveConfig = new (this._originalConfig.constructor as new (
      values: Array<{ alias: string; value: unknown }>
    ) => UmbPropertyEditorConfigCollection)(configValues);
  }

  updated(changed: Map<string, unknown>) {
    if (changed.has("value")) {
      this._syncInnerValue();
    }
  }

  render() {
    return html`
      ${this._restrictionInfo?.hasRestrictions
        ? html`<div style="display:flex;align-items:center;gap:6px;padding:6px 9px;background-color:var(--uui-color-surface-emphasis);border:1px solid var(--uui-color-border);border-radius:var(--uui-border-radius);margin-bottom:9px;font-size:0.85em;color:var(--uui-color-text-alt);">
            <umb-icon name="icon-filter" style="font-size:1em;"></umb-icon>
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

declare global {
  interface HTMLElementTagNameMap {
    "block-grid-restricted": BlockGridRestrictedElement;
  }
}

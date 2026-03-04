/**
 * Block Grid (Restricted) — custom property editor UI element.
 *
 * This web component wraps Umbraco's native `umb-property-editor-ui-block-grid`
 * element and intercepts its configuration to enforce block restrictions. It acts
 * as a transparent proxy: the content editor sees a normal Block Grid, but the
 * "add content" catalogue only offers block types allowed by the restriction rules.
 *
 * ## How it works
 *
 * 1. Umbraco instantiates this element because the data type is configured to use
 *    the "Block Grid (Restricted)" property editor UI (see manifest.ts).
 *
 * 2. On load, the element consumes two Umbraco contexts:
 *    - UMB_AUTH_CONTEXT — provides the Bearer token for authenticated API calls
 *    - UMB_ENTITY_CONTEXT — provides the content node's unique key (GUID)
 *
 * 3. Once both contexts are available, it calls the `allowed-blocks/{nodeKey}`
 *    API endpoint to resolve which block types are permitted for this node
 *    (based on the document type's restriction rule or inherited ancestor rules).
 *
 * 4. The restriction is applied by modifying the `blocks` config array:
 *    - Allowed blocks: left unchanged (can be added at root and in areas)
 *    - Disallowed blocks: `allowAtRoot` and `allowInAreas` set to `false`
 *    This means disallowed blocks won't appear in the "add content" catalogue,
 *    but existing instances of those blocks can still render and be edited.
 *    This is important: if a block was added before a restriction was applied,
 *    the content author can still see and edit it — they just can't add new ones.
 *
 * 5. The native Block Grid element is created imperatively (not via template)
 *    because it needs to live in the Light DOM for Umbraco's context propagation
 *    to work correctly. Shadow DOM would prevent context consumers in the native
 *    element from finding their providers higher up the DOM tree.
 *
 * ## The recreate-on-restriction pattern
 *
 * The native block grid manager caches the list of available block types from the
 * first config it receives. If the restriction data arrives after the inner element
 * was already created with the unrestricted config, we must destroy and recreate
 * the inner element so the manager initialises fresh with the restricted config.
 * This is the key insight that makes the restriction enforcement reliable.
 *
 * ## Value proxying
 *
 * The inner element's `property-value-change` events are intercepted and re-dispatched
 * from this element, so Umbraco's property system sees value changes as coming from
 * the registered property editor UI element (this one), not from the hidden inner one.
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
 * The `@customElement` decorator registers this class with the browser's custom
 * element registry under the tag name "block-grid-restricted". This tag name must
 * match the `elementName` in the manifest registration (see manifest.ts).
 *
 * `UmbElementMixin(LitElement)` adds Umbraco's context API capabilities (consumeContext,
 * observe, provideContext) to the standard LitElement base class.
 *
 * `implements UmbPropertyEditorUiElement` ensures this class satisfies Umbraco's
 * contract for property editor UI elements (value, config, readonly properties).
 */
@customElement("block-grid-restricted")
export default class BlockGridRestrictedElement
  extends UmbElementMixin(LitElement)
  implements UmbPropertyEditorUiElement
{
  /**
   * The property value — the Block Grid's JSON data (layout + content + settings).
   * Umbraco sets this when loading the content node and reads it back on save.
   * The `{ attribute: false }` option means this is only set programmatically,
   * not via an HTML attribute (the value is a complex object, not a string).
   */
  @property({ attribute: false })
  value: unknown;

  /**
   * The restriction data returned by the API, or null if no restrictions apply.
   * Decorated with `@state()` so Lit re-renders the info banner when this changes.
   */
  @state() private _restrictionInfo: AllowedBlocksResponse | null = null;

  /** The unmodified config from Umbraco — preserved as the "source of truth". */
  private _originalConfig?: UmbPropertyEditorConfigCollection;

  /** The config with restrictions applied — this is what the inner element receives. */
  private _effectiveConfig?: UmbPropertyEditorConfigCollection;

  /** The content node's unique key (GUID), obtained from UMB_ENTITY_CONTEXT. */
  private _entityKey: string | undefined;

  /**
   * The document type key for the content being edited. Obtained from
   * UMB_DOCUMENT_WORKSPACE_CONTEXT. Used as a fallback for new content
   * (where the node doesn't exist in the content tree yet) so the server
   * can check for a direct restriction rule on this document type.
   */
  private _contentTypeKey: string | undefined;

  /**
   * The parent node key when creating new content. Obtained from
   * UMB_PARENT_ENTITY_CONTEXT. Used as a fallback for new content so the
   * server can walk up the content tree from the parent to find inherited rules.
   */
  private _parentKey: string | undefined;

  /** Whether the auth context has been consumed and the API client configured. */
  private _authReady = false;

  /**
   * Whether this element is in a content editing context (Content section).
   * Set to true when UMB_DOCUMENT_WORKSPACE_CONTEXT resolves, which only
   * happens in the Content section. Prevents API calls when the property
   * editor is instantiated in Settings (e.g. document type previews).
   */
  private _isContentContext = false;

  /** Reference to the imperatively-created native block grid element. */
  private _innerElement: HTMLElement | null = null;

  /** Whether the inner element has been created and is ready for config/value sync. */
  private _innerReady = false;

  /**
   * Config setter — called by Umbraco when the property editor configuration is available.
   * Stores the original config, applies any known restrictions, and syncs to the inner element.
   */
  set config(value: UmbPropertyEditorConfigCollection | undefined) {
    this._originalConfig = value;
    this._applyRestrictions();
    this._syncInnerConfig();
  }

  /**
   * Config getter — returns the effective (restricted) config if available,
   * falling back to the original config. This ensures any consumer reading
   * `this.config` gets the restricted version.
   */
  get config(): UmbPropertyEditorConfigCollection | undefined {
    return this._effectiveConfig ?? this._originalConfig;
  }

  /**
   * Renders into Light DOM instead of Shadow DOM.
   *
   * This is critical for Umbraco's context system to work. Umbraco contexts
   * propagate via DOM events that bubble up through the tree. Shadow DOM creates
   * a boundary that would prevent the native block grid element (which is a child
   * of this element) from finding context providers higher up in the document.
   *
   * By returning `this` instead of a shadow root, all child elements share the
   * same DOM tree and context propagation works transparently.
   */
  protected override createRenderRoot() {
    return this;
  }

  /**
   * Constructor — sets up Umbraco context consumers.
   *
   * Two contexts are consumed in parallel (they may resolve in any order):
   *
   * 1. UMB_AUTH_CONTEXT — provides OpenAPI configuration (token, base URL, credentials)
   *    needed for authenticated API calls to our .NET backend.
   *
   * 2. UMB_ENTITY_CONTEXT — provides the content node's unique identifier.
   *    We observe its `unique` property reactively because it may change if the
   *    same editor instance is reused for a different node (e.g. in infinite editing).
   *
   * Both must be ready before we can call the API, so each consumer calls `_tryLoad()`
   * which checks that both prerequisites are met.
   */
  constructor() {
    super();

    // Consume the auth context to configure the API client with the Bearer token.
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

    // Consume the entity context to get the content node's unique key (GUID).
    // The `observe` call sets up a reactive subscription — if the unique key
    // changes (e.g. navigating between content nodes), restrictions are re-loaded.
    this.consumeContext(UMB_ENTITY_CONTEXT, (context) => {
      if (!context) return;
      this.observe(context.unique, (unique) => {
        if (unique) {
          this._entityKey = unique;
          this._tryLoad();
        }
      });
    });

    // Consume the document workspace context to get the content type key.
    // This context only exists in the Content section (not in Settings), so its
    // presence confirms we're editing actual content and should load restrictions.
    // Also provides the content type key as a fallback for new content nodes.
    this.consumeContext(UMB_DOCUMENT_WORKSPACE_CONTEXT, (context) => {
      if (!context) return;
      this._isContentContext = true;
      this._contentTypeKey = context.getContentTypeUnique() ?? undefined;
      this._tryLoad();
    });

    // Consume the parent entity context to get the parent node key.
    // For new content, the server uses this to walk up from the parent node
    // and find inherited restriction rules.
    this.consumeContext(UMB_PARENT_ENTITY_CONTEXT, (parentContext) => {
      if (!parentContext) return;
      const parent = parentContext.getParent();
      this._parentKey = parent?.unique ?? undefined;
    });
  }

  /**
   * Lifecycle: element connected to the DOM.
   *
   * Ensures the native block grid element is defined in the custom element
   * registry before creating our instance. This is non-trivial because Umbraco
   * lazy-loads property editor UI elements — the native block grid module is
   * only loaded when a property uses the native UI alias. Since our restricted
   * editor uses a different alias, the native module might never be loaded by
   * the extension system, causing `customElements.whenDefined()` to hang
   * indefinitely (particularly visible on hard page reloads).
   *
   * To work around this, we explicitly trigger the native module load via the
   * extension registry before falling back to `whenDefined`.
   */
  async connectedCallback() {
    super.connectedCallback();

    const tagName = "umb-property-editor-ui-block-grid";

    if (!customElements.get(tagName)) {
      // Native element not defined yet. Trigger loading of its module via the
      // extension registry. The manifest's `element` field is a lazy import
      // function that loads and registers the native custom element.
      try {
        const manifest = umbExtensionsRegistry.getByAlias(
          "Umb.PropertyEditorUi.BlockGrid",
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
   *
   * Cleans up the inner element to prevent memory leaks and stale event listeners.
   * Resets the ready flag so reconnection creates a fresh instance.
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
   * Creates (or recreates) the native block grid element imperatively.
   *
   * Why imperative creation instead of a Lit template?
   * - The element must exist in the Light DOM (not in a template's rendering scope)
   * - We need to set complex object properties (config, value) that can't be
   *   expressed as HTML attributes
   * - We need fine-grained control over when the element is created/destroyed
   *   (the recreate-on-restriction pattern)
   *
   * The element listens for `property-value-change` events from the inner element
   * and re-dispatches them with `bubbles: true` and `composed: true` so they
   * cross any shadow DOM boundaries and reach Umbraco's property system.
   */
  private _createInnerElement() {
    // Remove any existing inner element (used by the recreate-on-restriction pattern).
    if (this._innerElement) {
      this._innerElement.remove();
    }

    // Create a fresh instance of the native block grid editor.
    this._innerElement = document.createElement("umb-property-editor-ui-block-grid");

    // Proxy value change events: when the user edits blocks in the inner editor,
    // capture the new value and re-dispatch from this element so Umbraco sees it.
    this._innerElement.addEventListener("property-value-change", (e: Event) => {
      this.value = (e as CustomEvent).detail.value;
      this.dispatchEvent(
        new CustomEvent("property-value-change", { bubbles: true, composed: true })
      );
    });

    this._innerReady = true;

    // Apply the current config (restricted or original) and value to the new element.
    const config = this._effectiveConfig ?? this._originalConfig;
    if (config) {
      (this._innerElement as any).config = config;
    }
    (this._innerElement as any).value = this.value;

    // Trigger a Lit re-render so the template inserts the new element into the DOM.
    this.requestUpdate();
  }

  /**
   * Synchronises the config to the inner element.
   * Called when config changes after the inner element is already created.
   */
  private _syncInnerConfig() {
    if (!this._innerElement || !this._innerReady) return;
    const config = this._effectiveConfig ?? this._originalConfig;
    (this._innerElement as any).config = config;
  }

  /**
   * Synchronises the value to the inner element.
   * Called via Lit's `updated()` lifecycle when the `value` property changes
   * (e.g. when Umbraco loads a saved content node).
   */
  private _syncInnerValue() {
    if (!this._innerElement || !this._innerReady) return;
    (this._innerElement as any).value = this.value;
  }

  /**
   * Guards the API call until auth, entity key, and content context are all available.
   * The content context check ensures we only call the API when editing content
   * (Content section), not when the property editor is instantiated in Settings.
   */
  private _tryLoad() {
    if (this._authReady && this._entityKey && this._isContentContext) {
      this._loadRestrictions();
    }
  }

  /**
   * Fetches the effective block restrictions for this content node from the API.
   *
   * On success, applies the restrictions to the config and (if needed) recreates
   * the inner element. On failure, logs the error and leaves all blocks available
   * (fail-open behaviour — better to show too many blocks than none).
   */
  private async _loadRestrictions() {
    if (!this._entityKey) return;

    try {
      // Pass content type key and parent key as fallback context for new content.
      // For existing content, the server resolves by node key and ignores these.
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

    // The block grid manager caches block types from its first config.
    // If restrictions loaded after the inner element was already initialised,
    // recreate it so the manager starts fresh with the restricted config.
    if (this._innerReady && this._restrictionInfo?.hasRestrictions) {
      this._createInnerElement();
    }
  }

  /**
   * Applies restriction rules to the block configuration.
   *
   * This is the core restriction logic for Block Grid. Unlike Block List (which
   * filters blocks out entirely), Block Grid keeps ALL block type definitions
   * in the config but sets `allowAtRoot: false` and `allowInAreas: false` on
   * disallowed types. This approach:
   *
   * - Prevents disallowed blocks from appearing in the "add content" catalogue
   * - Allows existing instances of disallowed blocks to render and be edited
   *   (the block definition is still present so the editor knows how to render it)
   * - Preserves the block's settings, stylesheets, and other configuration
   *
   * The effective config is created as a new UmbPropertyEditorConfigCollection
   * instance (using the original's constructor) so Lit's change detection sees
   * it as a new value and triggers re-rendering.
   */
  private _applyRestrictions() {
    // No config yet — nothing to restrict.
    if (!this._originalConfig) {
      this._effectiveConfig = undefined;
      return;
    }

    // No restrictions in effect — use the original config as-is.
    if (!this._restrictionInfo?.hasRestrictions) {
      this._effectiveConfig = this._originalConfig;
      return;
    }

    // Build a Set of allowed content element type keys (lowercased for
    // case-insensitive comparison — GUIDs may have mixed case).
    const allowedKeys = new Set(
      this._restrictionInfo.allowedContentElementTypeKeys.map((k) =>
        k.toLowerCase()
      )
    );

    // Iterate through all config entries, modifying only the "blocks" array.
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
          // Disallowed: spread original properties but override the "allow" flags.
          return { ...block, allowAtRoot: false, allowInAreas: false };
        });
        configValues.push({ alias: entry.alias, value: modifiedBlocks });
      } else {
        // Non-block config entries (gridColumns, layoutStylesheet, etc.) pass through.
        configValues.push({ alias: entry.alias, value: entry.value });
      }
    }

    // Create a new config collection instance using the same constructor as the original.
    // This ensures compatibility with Umbraco's config collection API.
    this._effectiveConfig = new (this._originalConfig.constructor as new (
      values: Array<{ alias: string; value: unknown }>
    ) => UmbPropertyEditorConfigCollection)(configValues);
  }

  /**
   * Lit lifecycle: called after reactive properties change.
   * Syncs the `value` property to the inner element when Umbraco updates it
   * (e.g. when loading saved content or when another property editor changes the value).
   */
  updated(changed: Map<string, unknown>) {
    if (changed.has("value")) {
      this._syncInnerValue();
    }
  }

  /**
   * Renders the inner block grid element (or a loading spinner while it's being created).
   * The `${this._innerElement}` expression inserts the DOM node directly into
   * the template — Lit supports rendering actual DOM elements, not just strings.
   */
  render() {
    return html`
      ${this._innerElement ?? html`<uui-loader></uui-loader>`}
    `;
  }
}

/**
 * TypeScript global augmentation — registers the custom element tag name
 * so that `document.createElement("block-grid-restricted")` returns the
 * correct type and HTML templates get type checking for this element.
 */
declare global {
  interface HTMLElementTagNameMap {
    "block-grid-restricted": BlockGridRestrictedElement;
  }
}

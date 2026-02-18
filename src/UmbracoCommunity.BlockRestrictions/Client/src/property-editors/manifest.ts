/**
 * Property editor manifests — registers the restricted Block Grid and Block List
 * property editor UIs with the Umbraco backoffice extension system.
 *
 * Each editor needs several supporting manifests:
 *
 * 1. propertyEditorUi — the main registration (element, schema, settings)
 * 2. propertyContext (clipboard) — enables clipboard context for copy/paste
 * 3. propertyContext (sortMode) — enables sort mode context for drag/drop reordering
 * 4. propertyAction (copyToClipboard) — the "copy" action in the three-dot menu
 * 5. propertyAction (pasteFromClipboard) — the "paste" action in the three-dot menu
 * 6. propertyAction (sortMode) — the "enter sort mode" action in the three-dot menu
 *
 * Why are the contexts and actions needed?
 * The native block editors' contexts are filtered by `forPropertyEditorUis`.
 * Without registering our own aliases, clipboard/sort mode contexts wouldn't load
 * for our custom editor UIs, and the "add content" modal routing would fail silently.
 *
 * Both editors use the same underlying Umbraco property editor SCHEMA as the native
 * editors (Umbraco.BlockGrid / Umbraco.BlockList), so they share storage format,
 * configuration, and rendering. Only the UI wrapper is different.
 *
 * The meta.settings for each editor mirror the native editor's configuration
 * properties (live editing, editor width, grid columns, etc.) so they appear
 * in the data type configuration screen.
 */
export const manifests: Array<UmbExtensionManifest> = [
  // ─── Block Grid (Restricted) ──────────────────────────────────────────────

  // Main property editor UI registration
  {
    type: "propertyEditorUi",
    alias: "UmbracoCommunity.PropertyEditorUi.BlockGridRestricted",
    name: "Block Grid (Restricted)",
    elementName: "block-grid-restricted",
    js: () =>
      import("./block-grid-restricted/block-grid-restricted.element.js"),
    meta: {
      label: "Block Grid (Restricted)",
      icon: "icon-layout",
      group: "lists",
      // Use the native Block Grid schema — same storage format and config structure.
      propertyEditorSchemaAlias: "Umbraco.BlockGrid",
      supportsReadOnly: true,
      settings: {
        properties: [
          {
            alias: "blockGroups",
            label: "",
            propertyEditorUiAlias:
              "Umb.PropertyEditorUi.BlockTypeGroupConfiguration",
            weight: 1,
          },
          {
            alias: "useLiveEditing",
            label: "Live editing mode",
            description: "Live update content when editing in overlay",
            propertyEditorUiAlias: "Umb.PropertyEditorUi.Toggle",
          },
          {
            alias: "maxPropertyWidth",
            label: "Editor width",
            description:
              "Optional css overwrite. (example: 1200px or 100%)",
            propertyEditorUiAlias: "Umb.PropertyEditorUi.TextBox",
          },
          {
            alias: "createLabel",
            label: "Create Button Label",
            description:
              "Override the label text for adding a new block, Example Add Widget",
            propertyEditorUiAlias: "Umb.PropertyEditorUi.TextBox",
          },
          {
            alias: "gridColumns",
            label: "Grid Columns",
            description: "Set the number of columns for the layout.",
            propertyEditorUiAlias: "Umb.PropertyEditorUi.Integer",
            config: [
              { alias: "min", value: 0 },
              { alias: "placeholder", value: "12" },
            ],
          },
          {
            alias: "layoutStylesheet",
            label: "Layout Stylesheet",
            description:
              "Override default stylesheet for backoffice layout.",
            propertyEditorUiAlias:
              "Umb.PropertyEditorUi.BlockGridLayoutStylesheet",
            config: [{ alias: "singleItemMode", value: true }],
          },
        ],
      },
    },
  },

  // Clipboard context — enables copy/paste for Block Grid (Restricted)
  {
    type: "propertyContext",
    kind: "clipboard",
    alias: "UmbracoCommunity.PropertyContext.BlockGridRestricted.Clipboard",
    name: "Block Grid (Restricted) Clipboard Property Context",
    forPropertyEditorUis: [
      "UmbracoCommunity.PropertyEditorUi.BlockGridRestricted",
    ],
  },

  // Sort mode context — enables drag/drop reordering for Block Grid (Restricted)
  {
    type: "propertyContext",
    kind: "sortMode",
    alias: "UmbracoCommunity.PropertyContext.BlockGridRestricted.SortMode",
    name: "Block Grid (Restricted) Sort Mode Property Context",
    forPropertyEditorUis: [
      "UmbracoCommunity.PropertyEditorUi.BlockGridRestricted",
    ],
  },

  // Copy action — "Copy to clipboard" in the three-dot menu, shown when the property has a value
  {
    type: "propertyAction",
    kind: "copyToClipboard",
    alias:
      "UmbracoCommunity.PropertyAction.BlockGridRestricted.Clipboard.Copy",
    name: "Block Grid (Restricted) Copy To Clipboard Property Action",
    forPropertyEditorUis: [
      "UmbracoCommunity.PropertyEditorUi.BlockGridRestricted",
    ],
    conditions: [{ alias: "Umb.Condition.Property.HasValue" }],
  },

  // Paste action — "Paste from clipboard" in the three-dot menu, shown when writable
  {
    type: "propertyAction",
    kind: "pasteFromClipboard",
    alias:
      "UmbracoCommunity.PropertyAction.BlockGridRestricted.Clipboard.Paste",
    name: "Block Grid (Restricted) Paste From Clipboard Property Action",
    forPropertyEditorUis: [
      "UmbracoCommunity.PropertyEditorUi.BlockGridRestricted",
    ],
    conditions: [{ alias: "Umb.Condition.Property.Writable" }],
  },

  // Sort mode action — "Enter sort mode" in the three-dot menu, enables drag/drop reordering
  {
    type: "propertyAction",
    kind: "sortMode",
    alias: "UmbracoCommunity.PropertyAction.BlockGridRestricted.SortMode",
    name: "Block Grid (Restricted) Sort Mode Property Action",
    forPropertyEditorUis: [
      "UmbracoCommunity.PropertyEditorUi.BlockGridRestricted",
    ],
    conditions: [{ alias: "Umb.Condition.Property.HasValue" }],
  },

  // ─── Block List (Restricted) ──────────────────────────────────────────────

  // Main property editor UI registration
  {
    type: "propertyEditorUi",
    alias: "UmbracoCommunity.PropertyEditorUi.BlockListRestricted",
    name: "Block List (Restricted)",
    elementName: "block-list-restricted",
    js: () =>
      import("./block-list-restricted/block-list-restricted.element.js"),
    meta: {
      label: "Block List (Restricted)",
      icon: "icon-thumbnail-list",
      group: "lists",
      // Use the native Block List schema — same storage format and config structure.
      propertyEditorSchemaAlias: "Umbraco.BlockList",
      supportsReadOnly: true,
      settings: {
        properties: [
          {
            alias: "useSingleBlockMode",
            label: "Single block mode",
            description:
              "When in Single block mode, the output will be BlockListItem<>, instead of BlockListModel.\n\n**NOTE:**\nSingle block mode requires a maximum of one available block, and an amount set to minimum 1 and maximum 1 blocks.",
            propertyEditorUiAlias: "Umb.PropertyEditorUi.Toggle",
          },
          {
            alias: "useLiveEditing",
            label: "Live editing mode",
            description:
              "Live editing in editor overlays for live updated custom views or labels using custom expression.",
            propertyEditorUiAlias: "Umb.PropertyEditorUi.Toggle",
          },
          {
            alias: "useInlineEditingAsDefault",
            label: "Inline editing mode",
            description:
              "Use the inline editor as the default block view.",
            propertyEditorUiAlias: "Umb.PropertyEditorUi.Toggle",
          },
          {
            alias: "maxPropertyWidth",
            label: "Property editor width",
            description: "Optional CSS override, example: 800px or 100%",
            propertyEditorUiAlias: "Umb.PropertyEditorUi.TextBox",
          },
        ],
      },
    },
  },

  // Clipboard context — enables copy/paste for Block List (Restricted)
  {
    type: "propertyContext",
    kind: "clipboard",
    alias: "UmbracoCommunity.PropertyContext.BlockListRestricted.Clipboard",
    name: "Block List (Restricted) Clipboard Property Context",
    forPropertyEditorUis: [
      "UmbracoCommunity.PropertyEditorUi.BlockListRestricted",
    ],
  },

  // Sort mode context — enables drag/drop reordering for Block List (Restricted)
  {
    type: "propertyContext",
    kind: "sortMode",
    alias: "UmbracoCommunity.PropertyContext.BlockListRestricted.SortMode",
    name: "Block List (Restricted) Sort Mode Property Context",
    forPropertyEditorUis: [
      "UmbracoCommunity.PropertyEditorUi.BlockListRestricted",
    ],
  },

  // Copy action — shown when the property has a value
  {
    type: "propertyAction",
    kind: "copyToClipboard",
    alias:
      "UmbracoCommunity.PropertyAction.BlockListRestricted.Clipboard.Copy",
    name: "Block List (Restricted) Copy To Clipboard Property Action",
    forPropertyEditorUis: [
      "UmbracoCommunity.PropertyEditorUi.BlockListRestricted",
    ],
    conditions: [{ alias: "Umb.Condition.Property.HasValue" }],
  },

  // Paste action — "Paste from clipboard" in the three-dot menu, shown when writable
  {
    type: "propertyAction",
    kind: "pasteFromClipboard",
    alias:
      "UmbracoCommunity.PropertyAction.BlockListRestricted.Clipboard.Paste",
    name: "Block List (Restricted) Paste From Clipboard Property Action",
    forPropertyEditorUis: [
      "UmbracoCommunity.PropertyEditorUi.BlockListRestricted",
    ],
    conditions: [{ alias: "Umb.Condition.Property.Writable" }],
  },

  // Sort mode action — "Enter sort mode" in the three-dot menu, enables drag/drop reordering
  {
    type: "propertyAction",
    kind: "sortMode",
    alias: "UmbracoCommunity.PropertyAction.BlockListRestricted.SortMode",
    name: "Block List (Restricted) Sort Mode Property Action",
    forPropertyEditorUis: [
      "UmbracoCommunity.PropertyEditorUi.BlockListRestricted",
    ],
    conditions: [{ alias: "Umb.Condition.Property.HasValue" }],
  },

  // ─── Clipboard Value Translators ──────────────────────────────────────────
  //
  // These translators tell the clipboard system how to convert between the
  // property editor's block value format and the clipboard entry format.
  // Without them, the copy/paste actions appear in the menu but can't function.
  //
  // Since our restricted editors use the exact same value format as the native
  // editors (same schema, same JSON structure), our translators perform the
  // same transformations as the native ones.
  //
  // We use our own implementations (in ./translators/) rather than importing
  // the native translator classes because Vite's `external` config externalises
  // all @umbraco-cms imports. The native translators live at internal dist-cms
  // paths that aren't in Umbraco's runtime import map, so they can't resolve
  // at runtime. Our implementations import only from public API paths
  // (@umbraco-cms/backoffice/class-api) which ARE in the import map.

  // Block Grid (Restricted) → block clipboard format (copy)
  {
    type: "clipboardCopyPropertyValueTranslator",
    alias:
      "UmbracoCommunity.ClipboardCopyTranslator.BlockGridRestrictedToBlock",
    name: "Block Grid (Restricted) to Block Copy Translator",
    api: () => import("./translators/block-grid-to-block-copy.js"),
    fromPropertyEditorUi:
      "UmbracoCommunity.PropertyEditorUi.BlockGridRestricted",
    toClipboardEntryValueType: "block",
  } as unknown as UmbExtensionManifest,

  // block clipboard format → Block Grid (Restricted) (paste)
  {
    type: "clipboardPastePropertyValueTranslator",
    alias:
      "UmbracoCommunity.ClipboardPasteTranslator.BlockToBlockGridRestricted",
    name: "Block to Block Grid (Restricted) Paste Translator",
    weight: 900,
    api: () => import("./translators/block-to-block-grid-paste.js"),
    fromClipboardEntryValueType: "block",
    toPropertyEditorUi:
      "UmbracoCommunity.PropertyEditorUi.BlockGridRestricted",
  } as unknown as UmbExtensionManifest,

  // Block Grid (Restricted) → grid-block clipboard format (copy)
  {
    type: "clipboardCopyPropertyValueTranslator",
    alias:
      "UmbracoCommunity.ClipboardCopyTranslator.BlockGridRestrictedToGridBlock",
    name: "Block Grid (Restricted) to Grid Block Copy Translator",
    api: () => import("./translators/block-grid-to-grid-block-copy.js"),
    fromPropertyEditorUi:
      "UmbracoCommunity.PropertyEditorUi.BlockGridRestricted",
    toClipboardEntryValueType: "gridBlock",
  } as unknown as UmbExtensionManifest,

  // grid-block clipboard format → Block Grid (Restricted) (paste)
  {
    type: "clipboardPastePropertyValueTranslator",
    alias:
      "UmbracoCommunity.ClipboardPasteTranslator.GridBlockToBlockGridRestricted",
    name: "Grid Block to Block Grid (Restricted) Paste Translator",
    weight: 1000,
    api: () => import("./translators/grid-block-to-block-grid-paste.js"),
    fromClipboardEntryValueType: "gridBlock",
    toPropertyEditorUi:
      "UmbracoCommunity.PropertyEditorUi.BlockGridRestricted",
  } as unknown as UmbExtensionManifest,

  // Block List (Restricted) → block clipboard format (copy)
  {
    type: "clipboardCopyPropertyValueTranslator",
    alias:
      "UmbracoCommunity.ClipboardCopyTranslator.BlockListRestrictedToBlock",
    name: "Block List (Restricted) to Block Copy Translator",
    api: () => import("./translators/block-list-to-block-copy.js"),
    fromPropertyEditorUi:
      "UmbracoCommunity.PropertyEditorUi.BlockListRestricted",
    toClipboardEntryValueType: "block",
  } as unknown as UmbExtensionManifest,

  // block clipboard format → Block List (Restricted) (paste)
  {
    type: "clipboardPastePropertyValueTranslator",
    alias:
      "UmbracoCommunity.ClipboardPasteTranslator.BlockToBlockListRestricted",
    name: "Block to Block List (Restricted) Paste Translator",
    api: () => import("./translators/block-to-block-list-paste.js"),
    fromClipboardEntryValueType: "block",
    toPropertyEditorUi:
      "UmbracoCommunity.PropertyEditorUi.BlockListRestricted",
  } as unknown as UmbExtensionManifest,
];

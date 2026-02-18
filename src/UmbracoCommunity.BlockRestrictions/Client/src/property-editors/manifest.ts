export const manifests: Array<UmbExtensionManifest> = [
  // Block Grid (Restricted) property editor UI
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

  // Block Grid (Restricted) — clipboard and sort mode contexts
  {
    type: "propertyContext",
    kind: "clipboard",
    alias: "UmbracoCommunity.PropertyContext.BlockGridRestricted.Clipboard",
    name: "Block Grid (Restricted) Clipboard Property Context",
    forPropertyEditorUis: [
      "UmbracoCommunity.PropertyEditorUi.BlockGridRestricted",
    ],
  },
  {
    type: "propertyContext",
    kind: "sortMode",
    alias: "UmbracoCommunity.PropertyContext.BlockGridRestricted.SortMode",
    name: "Block Grid (Restricted) Sort Mode Property Context",
    forPropertyEditorUis: [
      "UmbracoCommunity.PropertyEditorUi.BlockGridRestricted",
    ],
  },
  {
    type: "propertyAction",
    kind: "copyToClipboard",
    alias:
      "UmbracoCommunity.PropertyAction.BlockGridRestricted.Clipboard.Copy",
    name: "Block Grid (Restricted) Copy To Clipboard Property Action",
    forPropertyEditorUis: [
      "UmbracoCommunity.PropertyEditorUi.BlockGridRestricted",
    ],
    conditions: [{ alias: "Umb.Condition.PropertyHasValue" }],
  },

  // Block List (Restricted) property editor UI
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

  // Block List (Restricted) — clipboard and sort mode contexts
  {
    type: "propertyContext",
    kind: "clipboard",
    alias: "UmbracoCommunity.PropertyContext.BlockListRestricted.Clipboard",
    name: "Block List (Restricted) Clipboard Property Context",
    forPropertyEditorUis: [
      "UmbracoCommunity.PropertyEditorUi.BlockListRestricted",
    ],
  },
  {
    type: "propertyContext",
    kind: "sortMode",
    alias: "UmbracoCommunity.PropertyContext.BlockListRestricted.SortMode",
    name: "Block List (Restricted) Sort Mode Property Context",
    forPropertyEditorUis: [
      "UmbracoCommunity.PropertyEditorUi.BlockListRestricted",
    ],
  },
  {
    type: "propertyAction",
    kind: "copyToClipboard",
    alias:
      "UmbracoCommunity.PropertyAction.BlockListRestricted.Clipboard.Copy",
    name: "Block List (Restricted) Copy To Clipboard Property Action",
    forPropertyEditorUis: [
      "UmbracoCommunity.PropertyEditorUi.BlockListRestricted",
    ],
    conditions: [{ alias: "Umb.Condition.PropertyHasValue" }],
  },
  {
    type: "propertyAction",
    kind: "pasteFromClipboard",
    alias:
      "UmbracoCommunity.PropertyAction.BlockListRestricted.Clipboard.Paste",
    name: "Block List (Restricted) Paste From Clipboard Property Action",
    forPropertyEditorUis: [
      "UmbracoCommunity.PropertyEditorUi.BlockListRestricted",
    ],
    conditions: [{ alias: "Umb.Condition.WritableProperty" }],
  },
];

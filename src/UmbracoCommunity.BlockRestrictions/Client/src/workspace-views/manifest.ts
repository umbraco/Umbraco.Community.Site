/**
 * Workspace view manifest — registers the "Blocks" tab on the Document Type workspace.
 *
 * This tab appears alongside the native tabs (Design, Composition, etc.) when editing
 * a document type in Settings. It provides the UI for configuring block restrictions.
 *
 * The condition ensures this view only appears on Document Type workspaces
 * (not Media Type, Member Type, etc.).
 */
export const manifests: Array<UmbExtensionManifest> = [
  {
    type: "workspaceView",
    alias: "UmbracoCommunity.WorkspaceView.DocumentType.BlockRestrictions",
    name: "Document Type Block Restrictions",
    elementName: "block-restrictions-workspace-view",
    js: () =>
      import("./block-restrictions/block-restrictions.element.js"),
    meta: {
      label: "Blocks",
      pathname: "blocks",
      icon: "icon-filter",
    },
    conditions: [
      {
        alias: "Umb.Condition.WorkspaceAlias",
        match: "Umb.Workspace.DocumentType",
      },
    ],
  },
];

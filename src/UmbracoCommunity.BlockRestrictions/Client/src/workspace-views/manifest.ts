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

/**
 * Manifests for the Block Restrictions File Import — a menu item under
 * Settings > Advanced that opens a workspace with the import UI.
 *
 * Uses the `kind: 'default'` workspace (no custom element/context needed)
 * with a single workspace view that renders the import dashboard.
 */

const WORKSPACE_ALIAS =
  "UmbracoCommunity.BlockRestrictions.FileImportWorkspace";

export const manifests: Array<UmbExtensionManifest> = [
  // Menu item in the Advanced sidebar group
  {
    type: "menuItem",
    alias: "UmbracoCommunity.BlockRestrictions.FileImportMenuItem",
    name: "Block Restrictions Import Menu Item",
    weight: 50,
    meta: {
      label: "Block Restrictions",
      icon: "icon-filter",
      entityType: "block-restrictions",
      menus: ["Umb.Menu.AdvancedSettings"],
    },
  },
  // Default workspace shell (provides heading + view container)
  {
    type: "workspace",
    kind: "default",
    alias: WORKSPACE_ALIAS,
    name: "Block Restrictions Import Workspace",
    meta: {
      entityType: "block-restrictions",
      headline: "Block Restrictions",
    },
  },
  // Single workspace view with the import UI
  {
    type: "workspaceView",
    alias: "UmbracoCommunity.BlockRestrictions.FileImportView",
    name: "Block Restrictions Import View",
    element: () => import("./file-import-dashboard.element.js"),
    weight: 100,
    meta: {
      label: "Import",
      pathname: "import",
      icon: "icon-filter",
    },
    conditions: [
      {
        alias: "Umb.Condition.WorkspaceAlias",
        match: WORKSPACE_ALIAS,
      },
    ],
  },
];

export const manifests: Array<UmbExtensionManifest> = [
  {
    type: "dashboard",
    name: "Sessionize Dashboard",
    alias: "UmbracoCommunity.Extensions.SessionizeDashboard",
    elementName: "sessionize-dashboard",
    js: () => import("./sessionize-dashboard.element.js"),
    meta: {
      label: "Sessionize",
      pathname: "sessionize",
    },
    conditions: [
      {
        alias: "Umb.Condition.SectionAlias",
        match: "Umb.Section.Content",
      },
    ],
  },
  {
    type: "dashboard",
    name: "Seed Export Dashboard",
    alias: "UmbracoCommunity.Extensions.SeedExportDashboard",
    elementName: "seed-export-dashboard",
    js: () => import("./seed-export-dashboard.element.js"),
    meta: {
      label: "Snapshot exports",
      pathname: "snapshot-exports",
    },
    conditions: [
      {
        alias: "Umb.Condition.SectionAlias",
        match: "Umb.Section.Settings",
      },
    ],
  },
];

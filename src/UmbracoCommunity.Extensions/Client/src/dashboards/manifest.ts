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
];

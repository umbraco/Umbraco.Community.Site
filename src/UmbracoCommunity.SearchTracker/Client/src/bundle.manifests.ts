export const manifests: Array<UmbExtensionManifest> = [
  {
    type: "dashboard",
    alias: "UmbracoCommunity.SearchTracker.Dashboard",
    name: "Search Terms",
    elementName: "search-tracker-dashboard",
    js: () => import("./dashboards/search-tracker-dashboard.element.js"),
    weight: 4,
    meta: {
      label: "Search Terms",
      pathname: "search-terms",
    },
    conditions: [
      {
        alias: "Umb.Condition.SectionAlias",
        match: "Umb.Section.Content",
      },
    ],
  },
];

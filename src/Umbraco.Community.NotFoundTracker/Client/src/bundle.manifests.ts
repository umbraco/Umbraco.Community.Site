export const manifests: Array<UmbExtensionManifest> = [
  {
    type: "dashboard",
    alias: "Umbraco.Community.NotFoundTracker.Dashboard",
    name: "404 Tracker",
    elementName: "not-found-tracker-dashboard",
    js: () => import("./dashboards/not-found-tracker-dashboard.element.js"),
    weight: 100,
    meta: {
      label: "404 Tracker",
      pathname: "not-found-tracker",
    },
    conditions: [
      {
        alias: "Umb.Condition.SectionAlias",
        match: "Umb.Section.Content",
      },
    ],
  },
];

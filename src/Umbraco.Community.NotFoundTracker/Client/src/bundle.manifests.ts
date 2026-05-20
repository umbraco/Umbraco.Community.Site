import "./dashboards/hits-tab.element.js";
import "./dashboards/ignore-rules-tab.element.js";

export const manifests: Array<UmbExtensionManifest> = [
  {
    type: "dashboard",
    alias: "Umbraco.Community.NotFoundTracker.Dashboard",
    name: "404 Tracker",
    elementName: "not-found-tracker-dashboard",
    js: () => import("./dashboards/not-found-tracker-dashboard.element.js"),
    weight: 5,
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
  {
    type: "modal",
    alias: "NotFoundTracker.Modal.AddIgnoreRule",
    name: "Add Ignore Rule Modal",
    element: () => import("./dashboards/modals/add-ignore-rule-modal.element.js"),
  },
  {
    type: "modal",
    alias: "NotFoundTracker.Modal.HitDetails",
    name: "Hit Details Modal",
    element: () => import("./dashboards/modals/hit-details-modal.element.js"),
  },
];

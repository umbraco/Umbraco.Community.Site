export const manifests: Array<UmbExtensionManifest> = [
  {
    name: "Umbraco Community Extensions Dashboard",
    alias: "UmbracoCommunity.Extensions.Dashboard",
    type: "dashboard",
    js: () => import("./dashboard.element.js"),
    meta: {
      label: "Example Dashboard",
      pathname: "example-dashboard",
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
    name: "Umbraco Community HQ Members Dashboard",
    alias: "UmbracoCommunity.Extensions.HqMembersDashboard",
    elementName: "hq-members-dashboard",
    js: () => import("./hq-members-dashboard.element.js"),
    meta: {
      label: "HQ Members",
      pathname: "hq-members",
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
    name: "Umbraco Community Contribution Stats Dashboard",
    alias: "UmbracoCommunity.Extensions.ContributionStatsDashboard",
    elementName: "contribution-stats-dashboard",
    js: () => import("./contribution-stats-dashboard.element.js"),
    meta: {
      label: "Contributions & Releases",
      pathname: "contribution-stats",
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
    name: "Data Management Dashboard",
    alias: "UmbracoCommunity.Extensions.DataManagementDashboard",
    elementName: "data-management-dashboard",
    js: () => import("./data-management-dashboard.element.js"),
    weight: 100,
    meta: {
      label: "Data Management",
      pathname: "data-management",
    },
    conditions: [
      {
        alias: "Umb.Condition.SectionAlias",
        match: "Umb.Section.Settings",
      },
    ],
  },
  {
    type: "dashboard",
    name: "Sessionize Settings Dashboard",
    alias: "UmbracoCommunity.Extensions.SessionizeSettingsDashboard",
    elementName: "sessionize-settings-dashboard",
    js: () => import("./sessionize-settings-dashboard.element.js"),
    weight: 90,
    meta: {
      label: "Sessionize",
      pathname: "sessionize",
    },
    conditions: [
      {
        alias: "Umb.Condition.SectionAlias",
        match: "Umb.Section.Settings",
      },
    ],
  },
];

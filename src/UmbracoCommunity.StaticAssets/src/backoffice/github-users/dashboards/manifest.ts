export const manifests = [
  {
    type: "dashboard",
    name: "Umbraco Community HQ Members Dashboard",
    alias: "UmbracoCommunity.GitHubUsers.HqMembersDashboard",
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
    alias: "UmbracoCommunity.GitHubUsers.ContributionStatsDashboard",
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
] as const;

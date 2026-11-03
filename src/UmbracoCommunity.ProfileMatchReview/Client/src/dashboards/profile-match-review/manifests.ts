import { REVIEWER_CONDITION_ALIAS } from "./is-reviewer.condition.js";

export const manifests: Array<UmbExtensionManifest> = [
  {
    type: "dashboard",
    name: "Profile Match Review Dashboard",
    alias: "UmbracoCommunity.ProfileMatchReview.Dashboard",
    elementName: "profile-match-review-dashboard",
    js: () => import("./profile-match-review-dashboard.element.js"),
    weight: 4,
    meta: {
      label: "Profile match review",
      pathname: "profile-match-review",
    },
    conditions: [
      {
        alias: "Umb.Condition.SectionAlias",
        match: "Umb.Section.Members",
      },
      {
        alias: REVIEWER_CONDITION_ALIAS,
      },
    ],
  },
];

export const conditionManifests: Array<UmbExtensionManifest> = [
  {
    type: "condition",
    name: "Profile Match Reviewer Condition",
    alias: REVIEWER_CONDITION_ALIAS,
    api: () => import("./is-reviewer.condition.js"),
  },
];

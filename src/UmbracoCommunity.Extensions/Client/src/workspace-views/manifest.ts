import { UMB_WORKSPACE_CONDITION_ALIAS } from "@umbraco-cms/backoffice/workspace";
import { UMB_MEMBER_WORKSPACE_ALIAS } from "@umbraco-cms/backoffice/member";
import { IS_EXTERNAL_MEMBER_CONDITION_ALIAS } from "../conditions/is-external-member.condition.js";

export const manifests: Array<UmbExtensionManifest> = [
  {
    type: "workspaceView",
    alias: "UmbracoCommunity.WorkspaceView.ExternalMemberGroups",
    name: "External Member Groups Workspace View",
    element: () =>
      import("./external-member-groups/external-member-groups-workspace-view.element.js"),
    weight: 250,
    meta: {
      label: "Groups",
      pathname: "member-groups",
      icon: "icon-users",
    },
    conditions: [
      {
        alias: UMB_WORKSPACE_CONDITION_ALIAS,
        match: UMB_MEMBER_WORKSPACE_ALIAS,
      },
      {
        alias: IS_EXTERNAL_MEMBER_CONDITION_ALIAS,
      },
    ],
  },
];

import { IS_BLOG_NODE_CONDITION_ALIAS } from "./is-blog-node.condition.js";
import { IS_EXTERNAL_MEMBER_CONDITION_ALIAS } from "./is-external-member.condition.js";

export const manifests: Array<UmbExtensionManifest> = [
  {
    type: "condition",
    alias: IS_BLOG_NODE_CONDITION_ALIAS,
    name: "Is Blog Node Condition",
    api: () => import("./is-blog-node.condition.js"),
  },
  {
    type: "condition",
    alias: IS_EXTERNAL_MEMBER_CONDITION_ALIAS,
    name: "Is External Member Condition",
    api: () => import("./is-external-member.condition.js"),
  },
];

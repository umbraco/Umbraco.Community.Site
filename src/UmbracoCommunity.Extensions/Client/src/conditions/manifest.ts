import { IS_BLOG_NODE_CONDITION_ALIAS } from "./is-blog-node.condition.js";

export const manifests: Array<UmbExtensionManifest> = [
  {
    type: "condition",
    alias: IS_BLOG_NODE_CONDITION_ALIAS,
    name: "Is Blog Node Condition",
    api: () => import("./is-blog-node.condition.js"),
  },
];

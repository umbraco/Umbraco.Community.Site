import { UMB_DOCUMENT_ENTITY_TYPE } from "@umbraco-cms/backoffice/document";
import { IS_BLOG_NODE_CONDITION_ALIAS } from "../../conditions/is-blog-node.condition.js";

export const manifests: Array<UmbExtensionManifest> = [
  {
    type: "entityAction",
    kind: "default",
    alias: "UmbracoCommunity.EntityAction.CreateBlogArticle",
    name: "Create Blog Article Entity Action",
    forEntityTypes: [UMB_DOCUMENT_ENTITY_TYPE],
    api: () => import("./create-blog-article.action.js"),
    meta: {
      icon: "icon-add",
      label: "Create Article",
    },
    conditions: [
      {
        alias: IS_BLOG_NODE_CONDITION_ALIAS,
      },
    ],
  },
];

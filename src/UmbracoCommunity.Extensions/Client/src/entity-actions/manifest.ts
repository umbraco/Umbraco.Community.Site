import { manifests as createBlogArticleManifests } from "./create-blog-article/manifest.js";

export const manifests: Array<UmbExtensionManifest> = [
  ...createBlogArticleManifests,
];

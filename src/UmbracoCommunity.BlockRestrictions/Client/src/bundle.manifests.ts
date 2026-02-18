import { manifests as workspaceViews } from "./workspace-views/manifest.js";
import { manifests as propertyEditors } from "./property-editors/manifest.js";

export const manifests: Array<UmbExtensionManifest> = [
  ...workspaceViews,
  ...propertyEditors,
];

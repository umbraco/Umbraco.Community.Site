/**
 * Bundle entry point — aggregates all extension manifests for the Umbraco backoffice.
 *
 * Umbraco discovers this file via the umbraco-package.json manifest in the
 * public/ folder, which points to the built output of this entry point.
 * All extension types (workspace views, property editors, contexts, actions)
 * are collected here and exported as a single array.
 */
import { manifests as workspaceViews } from "./workspace-views/manifest.js";
import { manifests as propertyEditors } from "./property-editors/manifest.js";
import { manifests as dashboards } from "./dashboards/manifest.js";

export const manifests: Array<UmbExtensionManifest> = [
  ...workspaceViews,
  ...propertyEditors,
  ...dashboards,
];

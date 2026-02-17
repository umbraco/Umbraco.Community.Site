import { manifests as dashboards } from "./dashboards/manifest.js";
import { manifests as conditions } from "./conditions/manifest.js";
import { manifests as entityActions } from "./entity-actions/manifest.js";

// Job of the bundle is to collate all the manifests from different parts of the extension and load other manifests
// We load this bundle from umbraco-package.json
export const manifests: Array<UmbExtensionManifest> = [
  ...dashboards,
  ...conditions,
  ...entityActions,
];

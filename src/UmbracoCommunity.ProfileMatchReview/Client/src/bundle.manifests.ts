import {
  conditionManifests,
  manifests as profileMatchReview,
} from "./dashboards/profile-match-review/manifests.js";

export const manifests: Array<UmbExtensionManifest> = [
  ...conditionManifests,
  ...profileMatchReview,
];

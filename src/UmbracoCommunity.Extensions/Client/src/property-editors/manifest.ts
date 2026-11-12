import { manifests as eventSchedule } from "./event-schedule/manifest.js";
import { manifests as youTubeVideo } from "./youtube-video/manifest.js";

export const manifests: Array<UmbExtensionManifest> = [...eventSchedule, ...youTubeVideo];

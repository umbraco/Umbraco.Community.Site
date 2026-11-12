export const manifests: Array<UmbExtensionManifest> = [
  {
    type: "propertyEditorUi",
    alias: "UmbracoCommunity.PropertyEditorUi.YouTubeVideo",
    name: "YouTube video",
    element: () => import("./youtube-video-editor.element.js"),
    elementName: "youtube-video-editor",
    meta: {
      label: "YouTube video",
      icon: "icon-video",
      group: "media",
      propertyEditorSchemaAlias: "UmbracoCommunity.YouTubeVideo",
    },
  },
];

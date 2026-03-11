export const manifests: Array<UmbExtensionManifest> = [
  {
    type: "propertyEditorUi",
    alias: "UmbracoCommunity.PropertyEditorUi.EventSchedule",
    name: "Event Schedule",
    element: () => import("./event-schedule-editor.element.js"),
    elementName: "event-schedule-editor",
    meta: {
      label: "Event Schedule",
      icon: "icon-calendar",
      group: "common",
      propertyEditorSchemaAlias: "Umbraco.Plain.Json",
      settings: {
        properties: [
          {
            alias: "venues",
            label: "Venues",
            description:
              'Define venue names, aliases, and colors. Enter as JSON array: [{"alias": "my-venue", "name": "My Venue", "color": "#283a97"}]',
            propertyEditorUiAlias: "Umb.PropertyEditorUi.TextArea",
          },
        ],
        defaultData: [
          {
            alias: "venues",
            value:
              '[{"alias": "venue-1", "name": "Venue 1", "color": "#283a97"}]',
          },
        ],
      },
    },
  },
];

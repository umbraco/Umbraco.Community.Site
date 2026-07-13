export const manifests: Array<UmbExtensionManifest> = [
  {
    type: "dashboard",
    name: "Blog Announcements Dashboard",
    alias: "UmbracoCommunity.BlogAnnouncements.Dashboard",
    elementName: "blog-announcements-dashboard",
    js: () => import("./blog-announcements-dashboard.element.js"),
    // Below the default content dashboard (higher weight sorts first; a low weight sorts this after the Umbraco welcome dashboard).
    weight: 5,
    meta: {
      label: "Blog Announcements",
      pathname: "blog-announcements",
    },
    conditions: [
      {
        alias: "Umb.Condition.SectionAlias",
        match: "Umb.Section.Content",
      },
    ],
  },
  {
    type: "modal",
    alias: "UmbracoCommunity.BlogAnnouncements.Modal.PostDetails",
    name: "Blog Announcement Post Details Modal",
    element: () => import("./modals/post-details-modal.element.js"),
  },
];

import { defineConfig } from "vite";

export default defineConfig({
  build: {
    lib: {
      entry: "src/bundle.manifests.ts",
      formats: ["es"],
      fileName: () => "blog-announcements.js",
    },
    outDir: "../wwwroot/App_Plugins/UmbracoCommunityBlogAnnouncements",
    emptyOutDir: true,
    sourcemap: true,
    rollupOptions: {
      external: [/^@umbraco-cms\/backoffice/],
    },
  },
});

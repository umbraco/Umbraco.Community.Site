import { defineConfig } from "vite";

export default defineConfig({
  build: {
    lib: {
      entry: "src/bundle.manifests.ts",
      formats: ["es"],
      fileName: () => "not-found-tracker.js",
    },
    outDir: "../wwwroot/App_Plugins/UmbracoCommunityNotFoundTracker",
    emptyOutDir: true,
    sourcemap: true,
    rollupOptions: {
      external: [/^@umbraco-cms\/backoffice/],
    },
  },
});

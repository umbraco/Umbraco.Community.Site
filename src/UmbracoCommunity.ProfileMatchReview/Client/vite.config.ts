import { defineConfig } from "vite";

export default defineConfig({
  build: {
    lib: {
      entry: "src/bundle.manifests.ts",
      formats: ["es"],
      fileName: () => "profile-match-review.js",
    },
    outDir: "../wwwroot/App_Plugins/UmbracoCommunityProfileMatchReview",
    emptyOutDir: true,
    sourcemap: true,
    rollupOptions: {
      external: [/^@umbraco-cms\/backoffice/],
    },
  },
});

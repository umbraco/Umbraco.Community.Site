import { defineConfig } from "vite";

export default defineConfig({
  build: {
    lib: {
      entry: "src/bundle.manifests.ts",
      formats: ["es"],
      fileName: "block-restrictions",
    },
    outDir: "../wwwroot/App_Plugins/UmbracoCommunityBlockRestrictions",
    emptyOutDir: true,
    sourcemap: true,
    rollupOptions: {
      external: [/^@umbraco/, /^lit/],
    },
  },
});

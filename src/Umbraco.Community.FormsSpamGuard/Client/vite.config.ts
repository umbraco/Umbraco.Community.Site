import { defineConfig } from "vite";

export default defineConfig({
  build: {
    lib: {
      entry: "src/bundle.manifests.ts",
      formats: ["es"],
      fileName: () => "forms-spam-guard.js",
    },
    outDir: "../wwwroot/App_Plugins/UmbracoCommunityFormsSpamGuard",
    // Safe to wipe: spam-guard.css/js and umbraco-package.json live in public/, which Vite re-copies on every
    // build. Leaving it false only accumulated superseded content-hashed chunks.
    emptyOutDir: true,
    sourcemap: true,
    rollupOptions: {
      external: [/^@umbraco-cms\/backoffice/],
    },
  },
});

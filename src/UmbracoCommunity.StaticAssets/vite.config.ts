import { defineConfig } from "vite";
import viteTSConfigPaths from "vite-tsconfig-paths";
import postcssPresetEnv from "postcss-preset-env";
import * as glob from "glob";
import mkcert from "vite-plugin-mkcert";
import atMixins from "postcss-mixins";
import calc from "postcss-calc";
import { getRhythm } from "./postcss-rhythm.mixin.js";
import { AcceptedPlugin } from "postcss";
import path from "path";
import { copyFileSync } from "fs";

export const postcssPlugins: AcceptedPlugin[] = [
  calc,
  atMixins({
    mixins: {
      rhythm: (mixin, property) => getRhythm(mixin, property),
    },
  }),
  postcssPresetEnv({
    features: {
      "custom-properties": {
        preserve: false,
      },
    },
  }),
];

const isBackoffice = process.env.BUILD_TARGET === "backoffice";

// Plugin to copy umbraco-package.json after build
const copyUmbracoPackageJson = () => ({
  name: 'copy-umbraco-package',
  closeBundle() {
    if (isBackoffice) {
      copyFileSync(
        'src/backoffice/github-users/umbraco-package.json',
        '../UmbracoCommunity.Web.UI/wwwroot/App_Plugins/UmbracoCommunityGitHubUsers/umbraco-package.json'
      );
    }
  }
});

export default defineConfig({
  build: isBackoffice
    ? {
        // Umbraco Backoffice Extension Build
        lib: {
          entry: "src/backoffice/github-users/bundle.manifests.ts",
          formats: ["es"],
          fileName: "umbraco-community-git-hub-users",
        },
        outDir: "../UmbracoCommunity.Web.UI/wwwroot/App_Plugins/UmbracoCommunityGitHubUsers",
        emptyOutDir: true,
        sourcemap: true,
        rollupOptions: {
          external: [/^@umbraco/],
        },
        copyPublicDir: false,
      }
    : {
        // Frontend Website Build
        manifest: true,
        outDir: "./dist",
        emptyOutDir: true,
        rollupOptions: {
          input: glob.sync("src/entrypoints/_*.ts").map((file) => file),
          output: {
            entryFileNames: "[name]-[hash].js",
            assetFileNames: (chunkInfo) => {
              if (chunkInfo.name?.indexOf("errorpage") !== -1)
                return "errorpage[extname]";

              return "[name]-[hash][extname]";
            },
            inlineDynamicImports: false,
          },
        },
      },
  css: {
    postcss: {
      plugins: [...postcssPlugins],
    },
  },
  plugins: [mkcert(), viteTSConfigPaths(), copyUmbracoPackageJson()],
  resolve: {
    alias: {
      "@umbraco-community": path.resolve(__dirname, "./src"),
    },
  },
});

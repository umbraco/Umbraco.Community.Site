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

export default defineConfig({
    build: {
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
    plugins: [mkcert(), viteTSConfigPaths()],
    resolve: {
        alias: {
            "@umbraco-community": path.resolve(__dirname, "./src"),
        },
    },
});

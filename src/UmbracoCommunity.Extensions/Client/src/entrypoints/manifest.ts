export const manifests: Array<UmbExtensionManifest> = [
  {
    name: "Umbraco Community Extensions Entrypoint",
    alias: "UmbracoCommunity.Extensions.Entrypoint",
    type: "backofficeEntryPoint",
    js: () => import("./entrypoint.js"),
  },
];

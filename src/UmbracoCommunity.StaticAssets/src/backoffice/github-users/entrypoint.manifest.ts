export const manifests = [
  {
    type: "backofficeEntryPoint",
    name: "Umbraco Community Git Hub Users Entrypoint",
    alias: "UmbracoCommunity.GitHubUsers.Entrypoint",
    js: () => import("./entrypoint.js"),
  },
] as const;

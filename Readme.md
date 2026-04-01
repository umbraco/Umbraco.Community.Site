# Umbraco Community Website

The source of the new Umbraco community site that is currently being built to replace [community.umbraco.com](https://community.umbraco.com).

## Local Development

Clone the repository locally.

### Configuration

If you need to work with valid API keys for services, or to store connection strings to local databases, you should create a copy of the `appsettings.Development.json` file and name it  `appsettings.Local.json`.

The file is ignored from source control.

### Building Everything

From the repository root:

```bash
node build.mjs local
```

This builds all frontend projects (backoffice extensions and static assets) for deployment.

### Running the Solution Locally

The quickest way to get everything running for development:

```bash
node build.mjs dev:dotnet
```

This builds the backoffice extensions, then starts the Vite dev server and `dotnet run` together with color-coded output. The default launch profile uses the `Development` environment.

If you prefer to run things separately (or run without the build script), you need two processes:

- cd to `src/UmbracoCommunity.Web.UI` and run `dotnet run`
- In a separate terminal, cd to `src/UmbracoCommunity.StaticAssets` and run `npm run dev`
  - Run `npm ci` first if you get errors about missing packages

See [BUILD.md](BUILD.md) for all build script options and launch profiles.

### Upgrading Packages

A CLI tool is provided to update all NuGet packages and regenerate Umbraco Deploy schema files.

```bash
# Build the tool
dotnet build tools/upgrade-umbraco

# Preview what would be updated (recommended first step)
tools\upgrade-umbraco\bin\Debug\net10.0\upgrade-umbraco.exe update-packages --dry-run

# Update packages and regenerate Deploy schema in one step
tools\upgrade-umbraco\bin\Debug\net10.0\upgrade-umbraco.exe all
```

See [tools/upgrade-umbraco/README.md](tools/upgrade-umbraco/README.md) for full documentation.

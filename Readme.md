# Umbraco Community Website

The source of the new Umbraco community site that is currently being built to replace [community.umbraco.com](https://community.umbraco.com).

## Local Development

Clone the repository locally.

### Configuration

If you need to work with valid API keys for services, or to store connection strings to local databases, you should create a copy of the `appsettings.Development.json` file and name it  `appsettings.Local.json`.

The file is ignored from source control.

### Running the Solution Locally

We need two processes running - IIS to serve Umbraco, and Vite's dev server to provide the front end assets.

So to start the environment:

- cd to `src/UmbracoCommunity.Web.UI`
- `dotnet run`
- open a new console
- cd to `src/UmbracoCommunity.StaticAssets`
- `npm run dev`
- `npm ci` (if you get an error due to missing components on the previous command)

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

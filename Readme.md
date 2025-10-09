# Umbraco Community Website

The source of the new Umbraco community site that is currently being built to replace [community.umbraco.com](https://community.umbraco.com).

## Local Development

Clone the repository locally.

### Configuration

If you need to work with valid API keys for services, or to store connection strings to local databases, you should create a copy of the `appsettings.Development.json` file and name it  `appsettings.Local.json`.

The file is ignored from source control.

### Running the Solution Locally

To work on both front-end and back-end, we need two processes running - IIS to serve Umbraco, and Vite's dev server to provide the front end assets.

To start the environment, use one of these options:

- cd to `src/UmbracoDotCom.Web.UI`
- `dotnet run`
- open a new console
- cd to `src/UmbracoDotCom.StaticAssets`
- `npm run dev`
- `npm ci` (if you get an error due to missing components on the previous command)
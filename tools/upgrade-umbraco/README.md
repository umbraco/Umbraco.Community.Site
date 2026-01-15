# upgrade-umbraco

A cross-platform CLI tool to help upgrade Umbraco packages and regenerate Deploy schema files.

## Requirements

- .NET 10 SDK

## Usage

Build once, then run directly without the `--` separator:

```bash
# Build the tool
dotnet build tools/upgrade-umbraco

# Run directly (Windows)
tools\upgrade-umbraco\bin\Debug\net10.0\upgrade-umbraco.exe --help

# Run directly (Linux/Mac)
./tools/upgrade-umbraco/bin/Debug/net10.0/upgrade-umbraco --help
```

Or use `dotnet run` (requires `--` separator):

```bash
dotnet run --project tools/upgrade-umbraco -- --help
```

## Commands

### update-packages

Updates all NuGet package versions in `Directory.Packages.props` to their latest stable releases.

```bash
# Preview what would be updated (recommended first step)
upgrade-umbraco update-packages --dry-run

# Apply updates
upgrade-umbraco update-packages
```

**What happens:**
1. Locates `Directory.Packages.props` by searching up from the current directory
2. For each package, queries the NuGet API for available versions
3. Filters out pre-release versions
4. Updates the version attribute if a newer stable version exists
5. Saves the file (unless `--dry-run` is specified)

**Options:**
| Option | Description |
|--------|-------------|
| `--dry-run` | Show what would be updated without modifying files |

### export-schema

Starts the Umbraco site and triggers Umbraco Deploy to regenerate all `.uda` schema files.

```bash
# Run with defaults
upgrade-umbraco export-schema

# Custom URL and timeout
upgrade-umbraco export-schema --url https://localhost:5001 --timeout 180
```

**What happens:**
1. Locates the `UmbracoCommunity.Web.UI` project directory
2. Starts the site using `dotnet run --no-build`
3. Polls the site URL until it responds successfully (or times out)
4. Creates an empty `deploy-export` trigger file in `umbraco/Deploy/`
5. Waits for Umbraco Deploy to process the trigger and delete the file
6. Shuts down the site gracefully

**Options:**
| Option | Description |
|--------|-------------|
| `--url <url>` | URL to check for site readiness (default: `https://localhost:44383`) |
| `--timeout <seconds>` | How long to wait for site startup (default: `120`) |

### all

Runs both `update-packages` and `export-schema` in sequence.

```bash
# Full upgrade workflow
upgrade-umbraco all

# Preview package updates only (export-schema is skipped in dry-run mode)
upgrade-umbraco all --dry-run
```

## Typical Upgrade Workflow

```bash
# 1. Build the tool
dotnet build tools/upgrade-umbraco

# 2. Preview changes
tools\upgrade-umbraco\bin\Debug\net10.0\upgrade-umbraco.exe update-packages --dry-run

# 3. Apply package updates
tools\upgrade-umbraco\bin\Debug\net10.0\upgrade-umbraco.exe update-packages

# 4. Restore and build the solution
dotnet restore
dotnet build

# 5. Regenerate Deploy schema
tools\upgrade-umbraco\bin\Debug\net10.0\upgrade-umbraco.exe export-schema

# 6. Review changes
git status
git diff
```

## How Deploy Schema Export Works

Umbraco Deploy watches for trigger files in the `umbraco/Deploy/` directory. When a file named `deploy-export` is created, Deploy:

1. Reads the current schema from the database
2. Serializes all document types, data types, media types, etc. to `.uda` files
3. Writes files to `umbraco/Deploy/Revision/`
4. Deletes the trigger file when complete

This ensures the `.uda` files match the current database schema, which is important after upgrades that may have changed serialization formats.

## Troubleshooting

**"Could not find Directory.Packages.props"**
- Run the tool from within the repository directory

**"Could not find UmbracoCommunity.Web.UI project directory"**
- Ensure you're running from the repository root or a subdirectory

**"Timeout waiting for site to be ready"**
- Increase timeout: `--timeout 300`
- Check that the site builds and runs correctly: `dotnet run --project src/UmbracoCommunity.Web.UI`
- Verify the URL matches your launchSettings.json configuration

**"deploy-export file still exists"**
- The site may not have Umbraco Deploy configured correctly
- Check the site logs for errors during the export process

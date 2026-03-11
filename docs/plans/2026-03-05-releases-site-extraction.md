# Releases Site Extraction Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Move the releases feature from the Umbraco Community Site into a standalone Umbraco CMS site at `~/Dev/releases`, then remove releases code from the community site.

**Architecture:** Same as existing — Umbraco 17 CMS with virtual page controllers, SQLite for GitHub data, Hangfire for background sync, Vite for frontend assets. Four-project solution: Web.UI (host), Web (logic), StaticAssets (frontend), Extensions (backoffice).

**Tech Stack:** .NET 10, Umbraco CMS 17.2.0, Hangfire (Cultiv.Hangfire 5.2.0), Vite 7.x, TypeScript, Lit 3.x, PostCSS

---

## Phase 1: Create New Umbraco Solution

### Task 1: Scaffold the Umbraco host project

**Files:**
- Create: `~/Dev/releases/src/Releases.Web.UI/`

**Step 1: Create solution directory**
```bash
mkdir -p ~/Dev/releases/src
cd ~/Dev/releases
```

**Step 2: Create the Umbraco project**
```bash
cd ~/Dev/releases/src
dotnet new umbraco -n Releases.Web.UI --friendly-name "Administrator" --email "admin@releases.umbraco.com" --password "releases!" --development-database-type SQLite --no-restore
```

**Step 3: Create solution file**
```bash
cd ~/Dev/releases
dotnet new sln -n Releases
dotnet sln add src/Releases.Web.UI/Releases.Web.UI.csproj
```

**Step 4: Verify it builds**
```bash
dotnet build
```
Expected: Build succeeded

**Step 5: Commit**
```bash
cd ~/Dev/releases
git init
git add -A
git commit -m "feat: scaffold Umbraco 17 host project"
```

### Task 2: Create the Releases.Web class library

**Files:**
- Create: `~/Dev/releases/src/Releases.Web/Releases.Web.csproj`

**Step 1: Create the project**
```bash
cd ~/Dev/releases/src
dotnet new classlib -n Releases.Web -f net10.0
rm Releases.Web/Class1.cs
```

**Step 2: Add project reference from Web.UI → Web**
```bash
cd ~/Dev/releases
dotnet sln add src/Releases.Web/Releases.Web.csproj
cd src/Releases.Web.UI
dotnet add reference ../Releases.Web/Releases.Web.csproj
```

**Step 3: Commit**
```bash
cd ~/Dev/releases
git add -A
git commit -m "feat: add Releases.Web class library"
```

### Task 3: Create the Releases.Extensions Razor Class Library

**Files:**
- Create: `~/Dev/releases/src/Releases.Extensions/Releases.Extensions.csproj`

**Step 1: Create the project**
```bash
cd ~/Dev/releases/src
dotnet new razorclasslib -n Releases.Extensions -f net10.0
rm -r Releases.Extensions/wwwroot Releases.Extensions/Component1.razor Releases.Extensions/_Imports.razor Releases.Extensions/ExampleJsInterop.cs
```

**Step 2: Add project references**
```bash
cd ~/Dev/releases
dotnet sln add src/Releases.Extensions/Releases.Extensions.csproj
cd src/Releases.Web.UI
dotnet add reference ../Releases.Extensions/Releases.Extensions.csproj
cd ../Releases.Extensions
dotnet add reference ../Releases.Web/Releases.Web.csproj
```

**Step 3: Commit**
```bash
cd ~/Dev/releases
git add -A
git commit -m "feat: add Releases.Extensions razor class library"
```

### Task 4: Set up Central Package Management

**Files:**
- Create: `~/Dev/releases/Directory.Packages.props`
- Modify: All `.csproj` files to use central package management

**Step 1: Create Directory.Packages.props**

Create `~/Dev/releases/Directory.Packages.props`:
```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <!-- Umbraco -->
    <PackageVersion Include="Umbraco.Cms" Version="17.2.0" />
    <PackageVersion Include="Umbraco.Cms.Core" Version="17.2.0" />
    <PackageVersion Include="Umbraco.Cms.Infrastructure" Version="17.2.0" />
    <PackageVersion Include="Umbraco.Cms.Web.Common" Version="17.2.0" />
    <PackageVersion Include="Umbraco.Cms.Web.Website" Version="17.2.0" />
    <PackageVersion Include="Umbraco.Cms.Api.Common" Version="17.2.0" />
    <PackageVersion Include="Umbraco.Cms.Api.Management" Version="17.2.0" />

    <!-- Database -->
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.3" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Sqlite" Version="10.0.3" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.SqlServer" Version="10.0.3" />

    <!-- Background Jobs -->
    <PackageVersion Include="Cultiv.Hangfire" Version="5.2.0" />

    <!-- Security -->
    <PackageVersion Include="Joonasw.AspNetCore.SecurityHeaders" Version="6.0.0" />

    <!-- Other -->
    <PackageVersion Include="Markdig" Version="1.0.0" />
    <PackageVersion Include="Schema.NET" Version="13.0.0" />
  </ItemGroup>
</Project>
```

**Step 2: Update Releases.Web.csproj**

Replace `~/Dev/releases/src/Releases.Web/Releases.Web.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Cultiv.Hangfire" />
    <PackageReference Include="Joonasw.AspNetCore.SecurityHeaders" />
    <PackageReference Include="Markdig" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" />
    <PackageReference Include="Schema.NET" />
    <PackageReference Include="Umbraco.Cms.Core" />
    <PackageReference Include="Umbraco.Cms.Infrastructure" />
    <PackageReference Include="Umbraco.Cms.Web.Common" />
  </ItemGroup>
</Project>
```

**Step 3: Update Releases.Web.UI.csproj** — remove version attributes from Umbraco.Cms package (it will come from central management), add ManagePackageVersionsCentrally awareness.

Check the generated .csproj and remove explicit `Version=` from `PackageReference` items that are now in `Directory.Packages.props`.

**Step 4: Update Releases.Extensions.csproj**
```xml
<Project Sdk="Microsoft.NET.Sdk.Razor">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <AddRazorSupportForMvc>true</AddRazorSupportForMvc>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Umbraco.Cms.Web.Website" />
    <PackageReference Include="Umbraco.Cms.Web.Common" />
    <PackageReference Include="Umbraco.Cms.Api.Common" />
    <PackageReference Include="Umbraco.Cms.Api.Management" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\Releases.Web\Releases.Web.csproj" />
  </ItemGroup>
</Project>
```

**Step 5: Build to verify**
```bash
cd ~/Dev/releases
dotnet build
```

**Step 6: Commit**
```bash
git add -A
git commit -m "feat: set up central package management with all dependencies"
```

---

## Phase 2: Copy Shared Infrastructure to Releases.Web

### Task 5: Copy Constants, Models, and Interfaces

**Source:** `~/Dev/Umbraco.Community.Site/src/UmbracoCommunity.Web/`
**Target:** `~/Dev/releases/src/Releases.Web/`

**Step 1: Create directory structure**
```bash
cd ~/Dev/releases/src/Releases.Web
mkdir -p Models/Pages Models/ViewModels/Components Models/ServiceModels
mkdir -p ViewModelBuilders/Components ViewModelBuilders/Schema ViewModelBuilders/Pages
mkdir -p Attributes Extensions Middleware TagHelpers Utilities Services/Abstract
mkdir -p Vite/Models Vite/TagHelpers
mkdir -p Features/ReleaseOverview/Controllers Features/ReleaseOverview/Configuration Features/ReleaseOverview/Models
mkdir -p Features/GitHubSync/Configuration Features/GitHubSync/Infrastructure Features/GitHubSync/Jobs Features/GitHubSync/Models
mkdir -p Migrations
```

**Step 2: Copy shared infrastructure files** (these get namespace-renamed)

Copy from `UmbracoCommunity.Web` to `Releases.Web`:
- `Constants.Culture.cs` → `Constants.Culture.cs`
- `Constants.Security.cs` → `Constants.Security.cs`
- `Models/Pages/PageViewModelBase.cs`
- `Models/ViewModels/Components/MenuViewModel.cs`
- `Models/ViewModels/Components/MenuReleasesViewModel.cs`
- `Models/ViewModels/Components/FooterViewModel.cs`
- `Models/ServiceModels/SitemapElement.cs`
- `ViewModelBuilders/IViewModelBuilder.cs`
- `ViewModelBuilders/IPageViewModelDecorator.cs`
- `ViewModelBuilders/ViewModelBuilderBase.cs`
- `ViewModelBuilders/Components/MenuViewModelBuilder.cs`
- `ViewModelBuilders/Components/MenuReleasesViewModelBuilder.cs`
- `ViewModelBuilders/Components/FooterViewModelBuilder.cs`
- `ViewModelBuilders/Schema/OrganizationSchemaBuilder.cs`
- `ViewModelBuilders/Schema/BreadcrumbSchemaBuilder.cs`
- `Attributes/ApplyFilterBase.cs`
- `Attributes/ApplyCommonElementsAttribute.cs`
- `Attributes/ApplyCommonElementsReleasesAttribute.cs`
- `Attributes/ApplyPageMetaDataAttribute.cs`
- `Extensions/CspBuilderExtensions.cs`
- `Extensions/UmbracoBuilderExtensions.cs` (will be trimmed to releases-only registrations)
- `Extensions/WebApplicationExtensions.cs` (trimmed)
- `Middleware/DisableCspMiddleware.cs`
- `TagHelpers/SvgTagHelper.cs`
- `Vite/Models/ViteManifest.cs`
- `Vite/Models/ViteManifestEntry.cs`
- `Vite/TagHelpers/NonceTagHelper.cs`
- `Vite/TagHelpers/ViteTagHelperBase.cs`
- `Vite/TagHelpers/ViteScriptTagHelper.cs`
- `Vite/TagHelpers/ViteLinkTagHelper.cs`
- `Services/Abstract/IContentDataService.cs`
- `Services/ContentDataService.cs`

**Step 3: Rename all namespaces**

In every copied file, find-and-replace:
- `UmbracoCommunity.Web` → `Releases.Web`
- `UmbracoCommunity.Web.Models` → `Releases.Web.Models`
- etc.

Use: `find ~/Dev/releases/src/Releases.Web -name "*.cs" -exec sed -i 's/UmbracoCommunity\.Web/Releases.Web/g' {} +`

**Step 4: Build to verify**
```bash
cd ~/Dev/releases && dotnet build
```
Fix any compilation errors from missing types or references.

**Step 5: Commit**
```bash
git add -A
git commit -m "feat: copy shared infrastructure (base classes, Vite, CSP, attributes)"
```

### Task 6: Copy the ReleaseOverview feature

**Source:** `~/Dev/Umbraco.Community.Site/src/UmbracoCommunity.Web/Features/ReleaseOverview/`
**Target:** `~/Dev/releases/src/Releases.Web/Features/ReleaseOverview/`

**Step 1: Copy all ReleaseOverview files**

Copy entire directory tree:
- `Controllers/` — ReleaseController, ReleasesHomeController, AllReleasesController, CompareController
- `Configuration/` — ReleaseRouteComposer, AllReleasesRouteComposer, CompareRouteComposer
- `Models/` — all view models (ReleaseGroupViewModel, ReleaseCategoryViewModel, etc.)

**Step 2: Copy release page view models**
- `Models/Pages/ReleasePageViewModel.cs`
- `Models/Pages/ReleasesHomePageViewModel.cs`
- `Models/Pages/AllReleasesPageViewModel.cs`
- `Models/Pages/ComparePageViewModel.cs`

**Step 3: Copy release view model builders**
- `ViewModelBuilders/Pages/ReleasePageViewModelBuilder.cs`
- `ViewModelBuilders/Pages/ReleasesHomePageViewModelBuilder.cs`
- `ViewModelBuilders/Pages/AllReleasesPageViewModelBuilder.cs`
- `ViewModelBuilders/Pages/ComparePageViewModelBuilder.cs`

**Step 4: Copy release utilities**
- `Utilities/ReleaseDiscussionParser.cs`
- `Utilities/ReleaseLabelHelper.cs`
- `Utilities/SemVerHelper.cs`
- `Utilities/UrlUtilities.cs` (if used by release builders)

**Step 5: Rename namespaces**
```bash
find ~/Dev/releases/src/Releases.Web/Features -name "*.cs" -exec sed -i 's/UmbracoCommunity\.Web/Releases.Web/g' {} +
```

**Step 6: Build and fix**
```bash
cd ~/Dev/releases && dotnet build
```

**Step 7: Commit**
```bash
git add -A
git commit -m "feat: copy ReleaseOverview feature (controllers, routes, builders, models)"
```

### Task 7: Copy the GitHubSync feature

**Source:** `~/Dev/Umbraco.Community.Site/src/UmbracoCommunity.Web/Features/GitHubSync/`
**Target:** `~/Dev/releases/src/Releases.Web/Features/GitHubSync/`

**Step 1: Copy all GitHubSync files**

Copy entire directory tree:
- `Configuration/` — RegisterServices, JobsComposer, GitHubSyncOptions
- `Infrastructure/` — GitHubSqlStore, GitHubApiClient, NuGetApiClient
- `Jobs/` — All 8 job files
- `Models/` — All DTOs (GitHubPullRequest, GitHubIssue, GitHubDiscussion, etc.)

**Step 2: Rename namespaces**
```bash
find ~/Dev/releases/src/Releases.Web/Features/GitHubSync -name "*.cs" -exec sed -i 's/UmbracoCommunity\.Web/Releases.Web/g' {} +
```

**Step 3: Build and fix**
```bash
cd ~/Dev/releases && dotnet build
```

**Step 6: Commit**
```bash
git add -A
git commit -m "feat: copy GitHubSync feature (Hangfire jobs, API clients)"
```

### Task 8: Trim and wire up UmbracoBuilderExtensions and Program.cs

**Files:**
- Modify: `~/Dev/releases/src/Releases.Web/Extensions/UmbracoBuilderExtensions.cs`
- Modify: `~/Dev/releases/src/Releases.Web/Extensions/WebApplicationExtensions.cs`
- Modify: `~/Dev/releases/src/Releases.Web.UI/Program.cs`

**Step 1: Trim UmbracoBuilderExtensions.cs**

Remove all non-release registrations. Keep only:
- Release view model builders (ReleasePageViewModelBuilder, ReleasesHomePageViewModelBuilder, AllReleasesPageViewModelBuilder, ComparePageViewModelBuilder)
- MenuReleasesViewModelBuilder, MenuViewModelBuilder, FooterViewModelBuilder
- Schema builders (OrganizationSchemaBuilder, BreadcrumbSchemaBuilder)
- ReleaseDiscussionParser, UrlUtilities
- Output cache policies
- ContentDataService
- SEO decorator registrations

Remove:
- All blog-related builder registrations
- Sessionize-related registrations
- Article schema builder (blog-specific)
- Any other community-site-specific registrations

**Step 2: Trim WebApplicationExtensions.cs**

Remove:
- Blog middleware (BlogFolderRedirectMiddleware, BlogRssMiddleware)
- Any community-site-specific pipeline filters

Keep:
- Security headers setup
- CSP configuration
- Release pipeline filters

**Step 3: Update Program.cs**

Update `~/Dev/releases/src/Releases.Web.UI/Program.cs` to match the community site's composition:
```csharp
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddComposers()
    .AddOutputCaching()
    .AddSecurityPolicies()
    .AddViewModelBuildersAndDecorators()
    .AddPipelineFilters()
    .Build();

WebApplication app = builder.Build();
await app.BootUmbracoAsync();

app.UseHttpsRedirection();
app.UseSecurityHeaders();
app.UseOutputCache();

app.UseUmbraco()
    .WithMiddleware(u =>
    {
        u.UseBackOffice();
        u.UseWebsite();
    })
    .WithEndpoints(u =>
    {
        u.UseBackOfficeEndpoints();
        u.UseApplicationEndpoints();
        u.UseWebsiteEndpoints();
    });

await app.RunAsync();
```

**Step 4: Build and fix all compilation errors**
```bash
cd ~/Dev/releases && dotnet build
```

**Step 5: Commit**
```bash
git add -A
git commit -m "feat: wire up Program.cs and trim extension methods to releases-only"
```

---

## Phase 3: Copy Views and Frontend Assets

### Task 9: Copy Razor views

**Source:** `~/Dev/Umbraco.Community.Site/src/UmbracoCommunity.Web.UI/Views/`
**Target:** `~/Dev/releases/src/Releases.Web.UI/Views/`

**Step 1: Copy release views**
- `LayoutReleases.cshtml`
- `SingleRelease.cshtml`
- `HomeReleases.cshtml`
- `AllReleases.cshtml`

**Step 2: Copy release partials**
```bash
mkdir -p ~/Dev/releases/src/Releases.Web.UI/Views/Partials/ReleaseOverview
mkdir -p ~/Dev/releases/src/Releases.Web.UI/Views/Partials/Components
```

Copy `Views/Partials/ReleaseOverview/`:
- `_SingleReleaseDetails.cshtml`
- `_ReleaseNotesHeader.cshtml`
- `_ReleaseCategories.cshtml`
- `_ContributorsSection.cshtml`
- `_ReleaseCard.cshtml`
- `_ReleasesOverview.cshtml`
- `_UpcomingReleaseItem.cshtml`
- `_PreReleaseNotice.cshtml`
- `_PullRequestItem.cshtml`
- `_CompareItem.cshtml`

Copy `Views/Partials/Components/`:
- `MenuReleases.cshtml`
- `HeaderReleases.cshtml`
- `FooterReleases.cshtml`
- `LogoReleases.cshtml`
- `LogoSvgReleases.cshtml`
- `MetaTags.cshtml` (shared but needed)

**Step 3: Update _ViewImports.cshtml**

Update `~/Dev/releases/src/Releases.Web.UI/Views/_ViewImports.cshtml` to include:
```razor
@using Joonasw.AspNetCore.SecurityHeaders.Csp
@using Releases.Web
@using Releases.Web.Models
@using Releases.Web.Models.Pages
@using Releases.Web.Models.ViewModels.Components
@using Releases.Web.Features.ReleaseOverview.Models
@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers
@addTagHelper *, Releases.Web
@addTagHelper *, Joonasw.AspNetCore.SecurityHeaders
@inject ICspNonceService Csp
@inject IWebHostEnvironment HostEnvironment
```

**Step 4: Update namespace references in views**

Find-and-replace `UmbracoCommunity.Web` → `Releases.Web` in all `.cshtml` files:
```bash
find ~/Dev/releases/src/Releases.Web.UI/Views -name "*.cshtml" -exec sed -i 's/UmbracoCommunity\.Web/Releases.Web/g' {} +
```

**Step 5: Build to verify**
```bash
cd ~/Dev/releases && dotnet build
```

**Step 6: Commit**
```bash
git add -A
git commit -m "feat: copy release views and partials"
```

### Task 10: Set up frontend assets (Vite)

**Files:**
- Create: `~/Dev/releases/src/Releases.StaticAssets/`

**Step 1: Create StaticAssets directory and initialize**
```bash
mkdir -p ~/Dev/releases/src/Releases.StaticAssets/src/entrypoints
mkdir -p ~/Dev/releases/src/Releases.StaticAssets/src/css
```

**Step 2: Copy package.json** from community site, trim to releases-only deps:

Create `~/Dev/releases/src/Releases.StaticAssets/package.json`:
```json
{
  "name": "releases-site",
  "private": true,
  "type": "module",
  "scripts": {
    "dev": "vite --port 5123",
    "build": "tsc -p tsconfig.build.json && vite build",
    "preview": "vite preview"
  },
  "dependencies": {
    "lit": "^3.3.0"
  },
  "devDependencies": {
    "typescript": "^5.0.2",
    "vite": "^7.1.3",
    "postcss": "^8.5.6",
    "postcss-mixins": "^12.1.2",
    "postcss-calc": "^10.1.1",
    "postcss-preset-env": "^10.2.4",
    "vite-tsconfig-paths": "^4.3.2",
    "@vitejs/plugin-basic-ssl": "^2.0.0",
    "glob": "^11.0.2"
  }
}
```

**Step 3: Copy Vite config, PostCSS rhythm mixin, and tsconfig**

From community site's `src/UmbracoCommunity.StaticAssets/`:
- `vite.config.ts` → adapt output paths
- `postcss-rhythm.mixin.ts` (if it's a standalone file)
- `tsconfig.json` and `tsconfig.build.json`

**Step 4: Copy entrypoints and CSS**
- `src/entrypoints/_releaseshome.ts`
- `src/css/releaseshome.css`

**Step 5: Install dependencies**
```bash
cd ~/Dev/releases/src/Releases.StaticAssets
npm ci
```

**Step 6: Verify frontend build**
```bash
npm run build
```

**Step 7: Commit**
```bash
cd ~/Dev/releases
git add -A
git commit -m "feat: set up Vite frontend with release assets"
```

---

## Phase 4: Copy Extensions Project

### Task 11: Copy releases-related Extensions code

**Source:** `~/Dev/Umbraco.Community.Site/src/UmbracoCommunity.Extensions/`
**Target:** `~/Dev/releases/src/Releases.Extensions/`

**Step 1: Copy C# files**

Copy only releases-related files:
- `Constants.cs`
- `Composers/UmbracoCommunityExtensionsApiComposer.cs` → rename to `ReleasesExtensionsApiComposer.cs`
- `Controllers/UmbracoCommunityExtensionsApiControllerBase.cs` → rename
- `Controllers/UmbracoCommunityExtensionsApiController.cs` → extract only `GetReleases()` and `GetContributionStats()` endpoints
- `Models/ReleaseInfo.cs`
- `Models/ReleaseSummary.cs`
- `Models/ContributionStats.cs`
- `Models/ContributorDetail.cs`

Do NOT copy:
- `Controllers/BlogArticleApiController.cs`
- `Models/CreateBlogArticleResponse.cs`
- Blog-related entity actions, conditions
- Sessionize dashboard

**Step 2: Copy Client code (releases-related dashboards only)**
```bash
mkdir -p ~/Dev/releases/src/Releases.Extensions/Client/src/dashboards
mkdir -p ~/Dev/releases/src/Releases.Extensions/Client/src/api
```

Copy:
- `Client/src/dashboards/contribution-stats-dashboard.element.ts`
- `Client/src/dashboards/data-management-dashboard.element.ts`
- `Client/src/dashboards/hq-members-dashboard.element.ts`
- `Client/src/dashboards/manifest.ts` (trim to releases-only dashboards)
- `Client/src/bundle.manifests.ts`
- `Client/package.json`, `Client/tsconfig.json`, `Client/vite.config.ts`
- `Client/src/api/` (generated API client)

Do NOT copy:
- `Client/src/dashboards/sessionize-dashboard.element.ts`
- `Client/src/entity-actions/create-blog-article/`
- `Client/src/conditions/is-blog-node.condition.ts`
- `Client/src/property-editors/event-schedule/`

**Step 3: Rename namespaces**
```bash
find ~/Dev/releases/src/Releases.Extensions -name "*.cs" -exec sed -i 's/UmbracoCommunity\.Extensions/Releases.Extensions/g' {} +
find ~/Dev/releases/src/Releases.Extensions -name "*.cs" -exec sed -i 's/UmbracoCommunity\.Web/Releases.Web/g' {} +
```

**Step 4: Update Constants.cs**
```csharp
namespace Releases.Extensions
{
    public class Constants
    {
        public const string ApiName = "releasesextensions";
    }
}
```

**Step 5: Create wwwroot directory for built assets**
```bash
mkdir -p ~/Dev/releases/src/Releases.Extensions/wwwroot/App_Plugins/ReleasesExtensions
```

**Step 6: Build and fix**
```bash
cd ~/Dev/releases && dotnet build
```

**Step 7: Commit**
```bash
git add -A
git commit -m "feat: copy releases-related Extensions project code"
```

---

## Phase 5: Configuration and Final Wiring

### Task 12: Set up configuration files

**Files:**
- Modify: `~/Dev/releases/src/Releases.Web.UI/appsettings.json`
- Modify: `~/Dev/releases/src/Releases.Web.UI/appsettings.Development.json`

**Step 1: Update appsettings.json**

Add GitHubSync and Hangfire configuration (copy from community site, remove Sessionize):
```json
{
  "GitHubSync": {
    "Token": "",
    "Organization": "umbraco",
    "RecentDays": 7,
    "Repositories": [
      {
        "Name": "Umbraco-CMS",
        "NuGetPackageIds": ["Umbraco.Cms"],
        "AnnouncementsPrefix": "cms"
      },
      { "Name": "Umbraco.UI" },
      { "Name": "Announcements" }
    ],
    "HqOnlyTeams": ["hq-only-teams"]
  },
  "Hangfire": {
    "UseStandaloneSection": true
  },
  "OutputCache": {
    "ContentDrivenDurationSeconds": 3600,
    "ExternalApiDurationSeconds": 300
  }
}
```

**Step 2: Verify appsettings.Development.json has SQLite connection strings**

Ensure both the Umbraco DB and GitHub DB connection strings are present:
```json
{
  "ConnectionStrings": {
    "umbracoDbDSN": "Data Source=|DataDirectory|/Umbraco.sqlite.db;Cache=Shared;Foreign Keys=True;Pooling=True",
    "umbracoDbDSN_ProviderName": "Microsoft.Data.Sqlite"
  },
  "OutputCache": {
    "ContentDrivenDurationSeconds": 60,
    "ExternalApiDurationSeconds": 30
  }
}
```

**Step 3: Add .gitignore**

Copy from community site or create with standard .NET + Node ignores, plus:
```
appsettings.Local.json
src/Releases.Web.UI/umbraco/
src/Releases.StaticAssets/dist/
src/Releases.StaticAssets/node_modules/
src/Releases.Extensions/Client/node_modules/
```

**Step 4: Commit**
```bash
cd ~/Dev/releases
git add -A
git commit -m "feat: configure appsettings for GitHub sync, Hangfire, and output cache"
```

### Task 13: Build, run, and verify the new site

**Step 1: Full solution build**
```bash
cd ~/Dev/releases
dotnet build
```
Fix all remaining compilation errors.

**Step 2: Run the site**
```bash
cd ~/Dev/releases/src/Releases.Web.UI
dotnet run
```
Expected: Umbraco boots, unattended install runs, site is accessible.

**Step 3: Verify Umbraco backoffice loads**

Navigate to `https://localhost:xxxxx/umbraco` and log in.

**Step 4: Verify frontend build**
```bash
cd ~/Dev/releases/src/Releases.StaticAssets
npm run build
```

**Step 5: Commit any fixes**
```bash
cd ~/Dev/releases
git add -A
git commit -m "fix: resolve compilation and runtime issues from extraction"
```

---

## Phase 6: Clean Up Community Site

### Task 14: Remove release features from community site

**Files to delete from `~/Dev/Umbraco.Community.Site/`:**

**Step 1: Delete release-specific features**
```bash
cd ~/Dev/Umbraco.Community.Site
rm -rf src/UmbracoCommunity.Web/Features/ReleaseOverview
rm -rf src/UmbracoCommunity.Web/Features/GitHubSync
```

**Step 2: Delete release page view models and builders**
```bash
rm src/UmbracoCommunity.Web/Models/Pages/ReleasePageViewModel.cs
rm src/UmbracoCommunity.Web/Models/Pages/ReleasesHomePageViewModel.cs
rm src/UmbracoCommunity.Web/Models/Pages/AllReleasesPageViewModel.cs
rm src/UmbracoCommunity.Web/Models/Pages/ComparePageViewModel.cs
rm src/UmbracoCommunity.Web/ViewModelBuilders/Pages/ReleasePageViewModelBuilder.cs
rm src/UmbracoCommunity.Web/ViewModelBuilders/Pages/ReleasesHomePageViewModelBuilder.cs
rm src/UmbracoCommunity.Web/ViewModelBuilders/Pages/AllReleasesPageViewModelBuilder.cs
rm src/UmbracoCommunity.Web/ViewModelBuilders/Pages/ComparePageViewModelBuilder.cs
```

**Step 3: Delete release menu components**
```bash
rm src/UmbracoCommunity.Web/Models/ViewModels/Components/MenuReleasesViewModel.cs
rm src/UmbracoCommunity.Web/ViewModelBuilders/Components/MenuReleasesViewModelBuilder.cs
```

**Step 4: Delete release attribute**
```bash
rm src/UmbracoCommunity.Web/Attributes/ApplyCommonElementsReleasesAttribute.cs
```

**Step 5: Delete release utilities**
```bash
rm src/UmbracoCommunity.Web/Utilities/ReleaseDiscussionParser.cs
rm src/UmbracoCommunity.Web/Utilities/ReleaseLabelHelper.cs
rm src/UmbracoCommunity.Web/Utilities/SemVerHelper.cs
```

**Step 6: Delete release views**
```bash
rm src/UmbracoCommunity.Web.UI/Views/LayoutReleases.cshtml
rm src/UmbracoCommunity.Web.UI/Views/SingleRelease.cshtml
rm src/UmbracoCommunity.Web.UI/Views/HomeReleases.cshtml
rm src/UmbracoCommunity.Web.UI/Views/AllReleases.cshtml
rm -rf src/UmbracoCommunity.Web.UI/Views/Partials/ReleaseOverview
rm src/UmbracoCommunity.Web.UI/Views/Partials/Components/MenuReleases.cshtml
rm src/UmbracoCommunity.Web.UI/Views/Partials/Components/HeaderReleases.cshtml
rm src/UmbracoCommunity.Web.UI/Views/Partials/Components/FooterReleases.cshtml
rm src/UmbracoCommunity.Web.UI/Views/Partials/Components/LogoReleases.cshtml
rm src/UmbracoCommunity.Web.UI/Views/Partials/Components/LogoSvgReleases.cshtml
```

**Step 7: Delete release frontend assets**
```bash
rm src/UmbracoCommunity.StaticAssets/src/entrypoints/_releaseshome.ts
rm src/UmbracoCommunity.StaticAssets/src/css/releaseshome.css
```

**Step 8: Commit deletions**
```bash
cd ~/Dev/Umbraco.Community.Site
git add -A
git commit -m "refactor: remove releases feature files (moved to standalone site)"
```

### Task 15: Clean up community site registrations and dependencies

**Files:**
- Modify: `src/UmbracoCommunity.Web/Extensions/UmbracoBuilderExtensions.cs`
- Modify: `src/UmbracoCommunity.Web/Extensions/WebApplicationExtensions.cs`
- Modify: `src/UmbracoCommunity.Web/UmbracoCommunity.Web.csproj`
- Modify: `src/UmbracoCommunity.Extensions/` (remove release controllers/models/dashboards)

**Step 1: Remove release builder registrations from UmbracoBuilderExtensions.cs**

Remove all `AddScoped` calls for:
- ReleasesHomePageViewModelBuilder
- ReleasePageViewModelBuilder
- AllReleasesPageViewModelBuilder
- ComparePageViewModelBuilder
- MenuReleasesViewModelBuilder
- ReleaseDiscussionParser

**Step 2: Remove GitHubSync-related composition**

Remove any Hangfire setup from extension methods if it was registered there (check if it's in composers or extension methods).

**Step 3: Remove unused package references**

Remove from `UmbracoCommunity.Web.csproj`:
- `Cultiv.Hangfire`

Also check `Directory.Packages.props` for orphaned versions.

**Step 4: Clean up Extensions project**

Remove release-related code:
- Remove `GetReleases()` and `GetContributionStats()` endpoints from the API controller
- Remove `ReleaseInfo.cs`, `ReleaseSummary.cs`, `ContributionStats.cs`, `ContributorDetail.cs` models
- Remove release dashboards from Client
- Update dashboard manifest to remove release dashboards

**Step 5: Build community site**
```bash
cd ~/Dev/Umbraco.Community.Site
dotnet build
```
Fix all compilation errors.

**Step 6: Run community site and verify**
```bash
cd ~/Dev/Umbraco.Community.Site/src/UmbracoCommunity.Web.UI
dotnet run
```
Verify other features (blog, sessionize) still work.

**Step 7: Commit**
```bash
cd ~/Dev/Umbraco.Community.Site
git add -A
git commit -m "refactor: remove release registrations, dependencies, and Extensions code"
```

### Task 16: Final verification of both sites

**Step 1: Build and run releases site**
```bash
cd ~/Dev/releases
dotnet build && cd src/Releases.Web.UI && dotnet run
```

**Step 2: Build and run community site**
```bash
cd ~/Dev/Umbraco.Community.Site
dotnet build && cd src/UmbracoCommunity.Web.UI && dotnet run
```

**Step 3: Verify no cross-references remain**

Check community site has no remaining references to release code:
```bash
cd ~/Dev/Umbraco.Community.Site
grep -r "ReleaseOverview\|GitHubSync\|ReleasesHome\|ReleaseController\|SemVerHelper\|ReleaseLabelHelper\|ReleaseDiscussionParser" src/ --include="*.cs" --include="*.cshtml"
```
Expected: No matches (or only comments/docs).

**Step 4: Commit any final fixes to both repos**

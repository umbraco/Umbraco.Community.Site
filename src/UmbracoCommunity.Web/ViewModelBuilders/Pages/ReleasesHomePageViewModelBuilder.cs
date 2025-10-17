using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;
using UmbracoCommunity.Web.Features.GitHubSync.Infrastructure;
using UmbracoCommunity.Web.Features.ReleaseOverview.Models;
using UmbracoCommunity.Web.Models.Pages;

namespace UmbracoCommunity.Web.ViewModelBuilders.Pages
{
    public class ReleasesHomePageViewModelBuilder : ViewModelBuilderBase, IViewModelBuilder<ReleasesHomePageViewModel>
    {
        private readonly GitHubSqlStore _dataStore;
        private readonly GitHubSyncOptions _options;
        private readonly Utilities.ReleaseDiscussionParser _releaseParser;

        public ReleasesHomePageViewModelBuilder(
            GitHubSqlStore dataStore,
            Microsoft.Extensions.Options.IOptions<GitHubSyncOptions> options,
            Utilities.ReleaseDiscussionParser releaseParser)
        {
            _dataStore = dataStore;
            _options = options.Value;
            _releaseParser = releaseParser;
        }

        public ReleasesHomePageViewModel Build(IPublishedContent currentPage, IUmbracoContext umbracoContext)
        {
            var viewModel = new ReleasesHomePageViewModel(currentPage);

            // Show upcoming releases timeline for Umbraco-CMS
            var defaultRepo = "Umbraco-CMS";
            PopulateReleaseTimeline(viewModel, defaultRepo);

            return viewModel;
        }
        private void PopulateReleaseTimeline(ReleasesHomePageViewModel viewModel, string repositoryName)
        {
            // Get all discussions for the "releases" category
            var discussions = _dataStore.GetDiscussionsByCategory(repositoryName, "releases").ToList();

            // Debug: Log the count
            System.Diagnostics.Debug.WriteLine(
                $"Found {discussions.Count} discussions for repository '{repositoryName}' in category 'releases'");

            if (!discussions.Any())
                return;

            // Get NuGet package versions from database (use as fallback for missing version info)
            // Find the NuGet package ID for this repository
            var repoConfig = _options.Repositories.FirstOrDefault(r => r.Name.Equals(repositoryName, StringComparison.OrdinalIgnoreCase));

            Dictionary<string, DateTime> nugetVersions = new();
            if (repoConfig?.HasNuGetPackage == true)
            {
                nugetVersions = _dataStore.GetNuGetPackageVersions(repoConfig.NuGetPackageId!);
            }

            // Parse discussions into release view models
            var allReleases = new List<ReleaseInfoViewModel>();
            foreach (var discussion in discussions)
            {
                System.Diagnostics.Debug.WriteLine($"Parsing discussion: {discussion.Title}");
                var releaseVm = _releaseParser.ParseReleaseInfo(discussion);
                if (releaseVm != null)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"  Parsed successfully: {releaseVm.Version}, ReleaseDate: {releaseVm.ReleaseDate}, IsTba: {releaseVm.IsReleaseDateTba}");

                    // Check if this version is available on NuGet
                    releaseVm.IsAvailableOnNuGet = nugetVersions.ContainsKey(releaseVm.Version);

                    allReleases.Add(releaseVm);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"  Failed to parse (returned null)");
                }
            }

            // For each release discussion, check if there's a newer released version in NuGet (excluding pre-releases)
            var now = DateTime.UtcNow;
            foreach (var release in allReleases)
            {
                var majorVersion = ParseVersion(release.ReleaseLabel).Major;

                // Get all stable (non-pre-release) NuGet versions for this major that have been released (date <= now)
                var latestNuGetVersion = nugetVersions
                    .Where(kvp => kvp.Value <= now)
                    .Select(kvp => new { VersionString = kvp.Key, SemVer = ParseSemVer(kvp.Key), PublishedDate = kvp.Value })
                    .Where(x => x.SemVer.Version.Major == majorVersion && string.IsNullOrEmpty(x.SemVer.PreRelease))
                    .OrderByDescending(x => x.SemVer.Version)
                    .FirstOrDefault();

                if (latestNuGetVersion != null && latestNuGetVersion.SemVer.Version > ParseVersion(release.ReleaseLabel))
                {
                    // There's a newer stable version in NuGet - update the discussion VM to point to it
                    release.ActualLatestVersion = latestNuGetVersion.VersionString;
                    release.ReleaseLabel = $"release/{latestNuGetVersion.VersionString}";
                    release.ReleaseDate = latestNuGetVersion.PublishedDate;
                    release.IsReleaseDateTba = false;
                    release.IsAvailableOnNuGet = true; // It's in NuGet
                }
            }

            // Sort by version (descending)
            allReleases = allReleases.OrderByDescending(r => ParseVersion(r.ReleaseLabel)).ToList();

            // Find latest release (highest version number among all released versions)
            viewModel.LatestRelease = allReleases
                .Where(r => r.IsReleased)
                .OrderByDescending(r => ParseVersion(r.ReleaseLabel))
                .FirstOrDefault();

            // Find LTS releases (all released LTS versions, sorted by version descending)
            viewModel.LtsReleases = allReleases
                .Where(r => r.IsReleased && r.IsLts)
                .OrderByDescending(r => ParseVersion(r.ReleaseLabel))
                .ToList();

            // Upcoming releases: not yet released, excluding the latest release
            viewModel.UpcomingReleases = allReleases
                .Where(r => !r.IsReleased)
                .OrderBy(r => !r.ReleaseDate.HasValue || r.IsReleaseDateTba ? 1 : 0) // Releases without dates go last
                .ThenBy(r => r.ReleaseDate ?? DateTime.MaxValue) // Sort by date (if they have one)
                .ThenByDescending(r => ParseVersion(r.ReleaseLabel)) // Sort by version for releases without dates
                .ToList();

            // Check for pre-release versions for upcoming releases
            foreach (var upcomingRelease in viewModel.UpcomingReleases)
            {
                var upcomingVersion = ParseSemVer(upcomingRelease.Version);

                // Find pre-release versions that match this major.minor.patch version
                var preReleaseVersion = nugetVersions
                    .Where(kvp => kvp.Value <= now)
                    .Select(kvp => new { VersionString = kvp.Key, SemVer = ParseSemVer(kvp.Key), PublishedDate = kvp.Value })
                    .Where(x => x.SemVer.Version.Major == upcomingVersion.Version.Major &&
                                x.SemVer.Version.Minor == upcomingVersion.Version.Minor &&
                                x.SemVer.Version.Build == upcomingVersion.Version.Build &&
                                !string.IsNullOrEmpty(x.SemVer.PreRelease))
                    .OrderByDescending(x => x.PublishedDate)
                    .FirstOrDefault();

                if (preReleaseVersion != null)
                {
                    upcomingRelease.HasPreRelease = true;
                    upcomingRelease.PreReleaseVersion = preReleaseVersion.VersionString;
                }
            }
        }

        private static (Version Version, string PreRelease) ParseSemVer(string versionString)
        {
            var dashIndex = versionString.IndexOf('-');
            if (dashIndex > 0)
            {
                var version = versionString.Substring(0, dashIndex);
                var preRelease = versionString.Substring(dashIndex + 1);
                return (Version.TryParse(version, out var v) ? v : new Version(0, 0, 0), preRelease);
            }

            return (Version.TryParse(versionString, out var ver) ? ver : new Version(0, 0, 0), string.Empty);
        }

        private static Version ParseVersion(string releaseLabel)
        {
            // Extract version from "release/X.Y.Z" format
            var versionString = releaseLabel.Replace("release/", "").Trim();

            // Use ParseSemVer to handle pre-release versions properly
            return ParseSemVer(versionString).Version;
        }
    }
}

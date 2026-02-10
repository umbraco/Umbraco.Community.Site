using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;
using UmbracoCommunity.Web.Features.GitHubSync.Infrastructure;
using UmbracoCommunity.Web.Features.ReleaseOverview.Models;
using UmbracoCommunity.Web.Models.Pages;
using UmbracoCommunity.Web.Utilities;

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

            // Get NuGet package versions from all configured packages
            Dictionary<string, DateTime> nugetVersions = new();
            var reposWithNuGet = _options.Repositories.Where(r => r.HasNuGetPackage).ToList();
            foreach (var repo in reposWithNuGet)
            {
                var packageIds = repo.GetNuGetPackageIds();
                foreach (var packageId in packageIds)
                {
                    var versions = _dataStore.GetNuGetPackageVersions(packageId);
                    foreach (var version in versions)
                    {
                        // If version doesn't exist or the new date is earlier, update it
                        if (!nugetVersions.ContainsKey(version.Key) || version.Value < nugetVersions[version.Key])
                        {
                            nugetVersions[version.Key] = version.Value;
                        }
                    }
                }
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
                var majorVersion = SemVerHelper.ParseReleaseLabel(release.ReleaseLabel).Major;

                // Get all stable (non-pre-release) NuGet versions for this major that have been released (date <= now)
                var latestNuGetVersion = nugetVersions
                    .Where(kvp => kvp.Value <= now)
                    .Select(kvp => new { VersionString = kvp.Key, SemVer = SemVerHelper.ParseToVersionWithPreRelease(kvp.Key), PublishedDate = kvp.Value })
                    .Where(x => x.SemVer.Version.Major == majorVersion && string.IsNullOrEmpty(x.SemVer.PreRelease))
                    .OrderByDescending(x => x.SemVer.Version)
                    .FirstOrDefault();

                if (latestNuGetVersion != null && latestNuGetVersion.SemVer.Version > SemVerHelper.ParseReleaseLabel(release.ReleaseLabel))
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
            allReleases = allReleases.OrderByDescending(r => SemVerHelper.ParseReleaseLabel(r.ReleaseLabel)).ToList();

            // Find latest release (highest version number among all released stable versions, excluding pre-releases)
            viewModel.LatestRelease = allReleases
                .Where(r => r.IsReleased && !SemVerHelper.IsPreRelease(r.Version))
                .OrderByDescending(r => SemVerHelper.ParseReleaseLabel(r.ReleaseLabel))
                .FirstOrDefault();

            // Find LTS releases (all released LTS versions, sorted by version descending, excluding pre-releases)
            viewModel.LtsReleases = allReleases
                .Where(r => r.IsReleased && r.IsLts && !SemVerHelper.IsPreRelease(r.Version))
                .OrderByDescending(r => SemVerHelper.ParseReleaseLabel(r.ReleaseLabel))
                .ToList();

            // Upcoming releases: not yet released, excluding pre-releases
            viewModel.UpcomingReleases = allReleases
                .Where(r => !r.IsReleased && !SemVerHelper.IsPreRelease(r.Version))
                .OrderBy(r => !r.ReleaseDate.HasValue || r.IsReleaseDateTba ? 1 : 0) // Releases without dates go last
                .ThenBy(r => r.ReleaseDate ?? DateTime.MaxValue) // Sort by date (if they have one)
                .ThenByDescending(r => SemVerHelper.ParseReleaseLabel(r.ReleaseLabel)) // Sort by version for releases without dates
                .ToList();

            // Check for pre-release versions for upcoming releases
            foreach (var upcomingRelease in viewModel.UpcomingReleases)
            {
                var upcomingVersion = SemVerHelper.ParseToVersionWithPreRelease(upcomingRelease.Version);

                // Find pre-release versions that match this major.minor.patch version
                var preReleaseVersion = nugetVersions
                    .Where(kvp => kvp.Value <= now)
                    .Select(kvp => new { VersionString = kvp.Key, SemVer = SemVerHelper.ParseToVersionWithPreRelease(kvp.Key), PublishedDate = kvp.Value })
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

    }
}

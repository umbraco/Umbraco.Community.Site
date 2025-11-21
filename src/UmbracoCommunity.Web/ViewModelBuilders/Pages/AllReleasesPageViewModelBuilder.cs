using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;
using UmbracoCommunity.Web.Features.GitHubSync.Infrastructure;
using UmbracoCommunity.Web.Features.ReleaseOverview.Models;
using UmbracoCommunity.Web.Models.Pages;
using UmbracoCommunity.Web.Utilities;

namespace UmbracoCommunity.Web.ViewModelBuilders.Pages;

internal class AllReleasesPageViewModelBuilder : ViewModelBuilderBase, IViewModelBuilder<AllReleasesPageViewModel>
{
    private readonly GitHubSqlStore _dataStore;
    private readonly GitHubSyncOptions _options;
    private readonly IMemoryCache _memoryCache;
    private readonly ReleaseDiscussionParser _releaseParser;

    public const string CacheKey = "AllReleases_VersionGroups";

    public AllReleasesPageViewModelBuilder(
        GitHubSqlStore dataStore,
        IOptions<GitHubSyncOptions> options,
        IMemoryCache memoryCache,
        ReleaseDiscussionParser releaseParser)
    {
        _dataStore = dataStore;
        _options = options.Value;
        _memoryCache = memoryCache;
        _releaseParser = releaseParser;
    }

    public AllReleasesPageViewModel Build(IPublishedContent currentPage, IUmbracoContext umbracoContext)
    {
        var viewModel = new AllReleasesPageViewModel(currentPage);

        // Cache the version groups since they don't change frequently
        var versionGroups = _memoryCache.GetOrCreate(CacheKey, entry =>
        {
            entry.Priority = CacheItemPriority.Normal;
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6); // Fallback expiration

            return BuildVersionGroups();
        });

        viewModel.VersionGroups = versionGroups!;

        return viewModel;
    }

    private List<MajorVersionGroupViewModel> BuildVersionGroups()
    {
        // Get all release discussions for Umbraco-CMS
        const string repositoryName = "Umbraco-CMS";
        var discussions = _dataStore.GetDiscussionsByCategory(repositoryName, "releases").ToList();

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

        // Parse all discussions into release view models
        var allReleases = new Dictionary<string, ReleaseInfoViewModel>();
        foreach (var discussion in discussions)
        {
            var releaseVm = _releaseParser.ParseReleaseInfo(discussion);
            if (releaseVm != null)
            {
                // Check if this version is available on NuGet
                releaseVm.IsAvailableOnNuGet = nugetVersions.ContainsKey(releaseVm.Version);
                allReleases[releaseVm.Version] = releaseVm;
            }
        }

        // Add all NuGet versions that don't have discussions
        var now = DateTime.UtcNow;
        foreach (var nugetVersion in nugetVersions.Where(kvp => kvp.Value <= now))
        {
            // Skip invalid semver versions
            if (!SemVerHelper.IsValidSemVer(nugetVersion.Key))
                continue;

            if (!allReleases.ContainsKey(nugetVersion.Key))
            {
                var releaseLabel = $"release/{nugetVersion.Key}";

                // Create a minimal release view model from NuGet data
                allReleases[nugetVersion.Key] = new ReleaseInfoViewModel
                {
                    Version = nugetVersion.Key,
                    ReleaseLabel = releaseLabel,
                    ReleaseDate = nugetVersion.Value,
                    IsReleaseDateTba = false,
                    IsLts = false, // We don't know if it's LTS from NuGet alone
                    Description = string.Empty,
                    DiscussionUrl = string.Empty,
                    IsAvailableOnNuGet = true // It's from NuGet
                };
            }
            else
            {
                // If the release exists from discussions but doesn't have a date,
                // update it with the NuGet release date
                var existingRelease = allReleases[nugetVersion.Key];
                if (!existingRelease.ReleaseDate.HasValue || existingRelease.IsReleaseDateTba)
                {
                    existingRelease.ReleaseDate = nugetVersion.Value;
                    existingRelease.IsReleaseDateTba = false;
                }
                // Mark as available on NuGet since we found it there
                existingRelease.IsAvailableOnNuGet = true;
            }
        }

        // Filter to only released versions (those already released on NuGet)
        var releasedVersions = allReleases.Values
            .Where(r => r.IsReleased)
            .OrderByDescending(r => ParseVersion(r.ReleaseLabel))
            .ToList();

        // Group by major version
        return releasedVersions
            .GroupBy(r => ParseVersion(r.ReleaseLabel).Major)
            .OrderByDescending(g => g.Key)
            .Select(g =>
            {
                // Sort all releases by SemVer
                var sortedReleases = g.OrderByDescending(r => r, new SemVerComparer()).ToList();

                // Get all non-pre-release versions for this major version
                var nonPreReleases = sortedReleases
                    .Where(r => !SemVerHelper.IsPreRelease(r.Version))
                    .ToList();

                // If we have non-pre-release versions, use the latest one as the featured release
                // Otherwise, don't show a featured release (all are pre-releases)
                var latestRelease = nonPreReleases.Any()
                    ? nonPreReleases.First()
                    : null;

                // All other releases (excluding the latest and pre-releases), ordered by version
                var otherReleases = sortedReleases
                    .Where(r => r != latestRelease && !SemVerHelper.IsPreRelease(r.Version))
                    .ToList();

                return new MajorVersionGroupViewModel
                {
                    MajorVersion = g.Key,
                    LatestRelease = latestRelease,
                    OtherReleases = otherReleases
                };
            })
            .Where(g => g.LatestRelease != null || g.OtherReleases.Any())
            .ToList();
    }

    private class SemVerComparer : IComparer<ReleaseInfoViewModel>
    {
        public int Compare(ReleaseInfoViewModel? x, ReleaseInfoViewModel? y)
        {
            if (x == null && y == null) return 0;
            if (x == null) return -1;
            if (y == null) return 1;

            var xVersion = ParseSemVer(x.Version);
            var yVersion = ParseSemVer(y.Version);

            // Compare major.minor.patch
            var versionCompare = xVersion.Version.CompareTo(yVersion.Version);
            if (versionCompare != 0) return versionCompare;

            // If versions are equal, compare pre-release tags
            // Stable releases (no pre-release) are greater than pre-releases
            if (string.IsNullOrEmpty(xVersion.PreRelease) && !string.IsNullOrEmpty(yVersion.PreRelease))
                return 1;
            if (!string.IsNullOrEmpty(xVersion.PreRelease) && string.IsNullOrEmpty(yVersion.PreRelease))
                return -1;

            // Both are pre-releases or both are stable
            return string.Compare(xVersion.PreRelease, yVersion.PreRelease, StringComparison.OrdinalIgnoreCase);
        }

        private (Version Version, string PreRelease) ParseSemVer(string versionString)
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
    }

    private static Version ParseVersion(string releaseLabel)
    {
        var versionString = releaseLabel.Replace("release/", "").Trim();

        // Handle pre-release versions (e.g., "14.0.0-rc1" -> "14.0.0")
        var dashIndex = versionString.IndexOf('-');
        if (dashIndex > 0)
        {
            versionString = versionString.Substring(0, dashIndex);
        }

        if (Version.TryParse(versionString, out var version))
        {
            return version;
        }

        return new Version(0, 0, 0);
    }
}

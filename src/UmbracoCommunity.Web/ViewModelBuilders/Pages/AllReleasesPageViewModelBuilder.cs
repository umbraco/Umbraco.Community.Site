using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;
using UmbracoCommunity.Web.Features.GitHubSync.Infrastructure;
using UmbracoCommunity.Web.Features.ReleaseOverview.Models;
using UmbracoCommunity.Web.Models.Pages;

namespace UmbracoCommunity.Web.ViewModelBuilders.Pages;

internal class AllReleasesPageViewModelBuilder : ViewModelBuilderBase, IViewModelBuilder<AllReleasesPageViewModel>
{
    private readonly GitHubSqlStore _dataStore;
    private readonly GitHubSyncOptions _options;
    private readonly IMemoryCache _memoryCache;

    public const string CacheKey = "AllReleases_VersionGroups";

    public AllReleasesPageViewModelBuilder(GitHubSqlStore dataStore, IOptions<GitHubSyncOptions> options, IMemoryCache memoryCache)
    {
        _dataStore = dataStore;
        _options = options.Value;
        _memoryCache = memoryCache;
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

        // Calculate release stats from PRs and Issues
        var allPrs = _dataStore.GetPullRequestsByLabelPattern(repositoryName, "release/").ToList();
        var allIssues = _dataStore.GetIssuesByLabelPattern(repositoryName, "release/").ToList();
        var releaseStats = CalculateReleaseStats(allPrs, allIssues);

        // Get NuGet package versions
        var repoConfig = _options.Repositories.FirstOrDefault(r =>
            r.Name.Equals(repositoryName, StringComparison.OrdinalIgnoreCase));

        Dictionary<string, DateTime> nugetVersions = new();
        if (repoConfig?.HasNuGetPackage == true)
        {
            nugetVersions = _dataStore.GetNuGetPackageVersions(repoConfig.NuGetPackageId!);
        }

        // Parse all discussions into release view models
        var allReleases = new Dictionary<string, ReleaseDiscussionViewModel>();
        foreach (var discussion in discussions)
        {
            var releaseVm = ParseReleaseDiscussion(discussion, releaseStats);
            if (releaseVm != null)
            {
                allReleases[releaseVm.Version] = releaseVm;
            }
        }

        // Add all NuGet versions that don't have discussions
        var now = DateTime.UtcNow;
        foreach (var nugetVersion in nugetVersions.Where(kvp => kvp.Value <= now))
        {
            if (!allReleases.ContainsKey(nugetVersion.Key))
            {
                var releaseLabel = $"release/{nugetVersion.Key}";
                var stats = releaseStats.GetValueOrDefault(releaseLabel, (0, 0, 0));
                var (features, issues, breaking) = stats;

                // Create a minimal release view model from NuGet data
                allReleases[nugetVersion.Key] = new ReleaseDiscussionViewModel
                {
                    Version = nugetVersion.Key,
                    ReleaseLabel = releaseLabel,
                    ReleaseDate = nugetVersion.Value,
                    IsReleaseDateTba = false,
                    IsLts = false, // We don't know if it's LTS from NuGet alone
                    Description = string.Empty,
                    FeatureCount = features,
                    IssueCount = issues,
                    BreakingChangesCount = breaking,
                    DiscussionUrl = string.Empty
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
                    .Where(r => !IsPreRelease(r.Version))
                    .ToList();

                // If we have non-pre-release versions, use the latest one as the featured release
                // Otherwise, don't show a featured release (all are pre-releases)
                var latestRelease = nonPreReleases.Any()
                    ? nonPreReleases.First()
                    : null;

                // All other releases (excluding the latest), ordered by version
                var otherReleases = sortedReleases
                    .Where(r => r != latestRelease)
                    .ToList();

                return new MajorVersionGroupViewModel
                {
                    MajorVersion = g.Key,
                    LatestRelease = latestRelease,
                    OtherReleases = otherReleases
                };
            })
            .ToList();
    }

    private static bool IsPreRelease(string version)
    {
        // A pre-release version contains a dash (e.g., "14.0.0-rc1")
        return version.Contains("-");
    }

    private class SemVerComparer : IComparer<ReleaseDiscussionViewModel>
    {
        public int Compare(ReleaseDiscussionViewModel? x, ReleaseDiscussionViewModel? y)
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

    private ReleaseDiscussionViewModel? ParseReleaseDiscussion(
        Features.GitHubSync.Models.GitHubDiscussion discussion,
        Dictionary<string, (int features, int issues, int breaking)> releaseStats)
    {
        var releaseLabel = discussion.Labels.FirstOrDefault(l => l.StartsWith("release/", StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrEmpty(releaseLabel))
            return null;

        var version = releaseLabel.Substring("release/".Length);

        DateTime? releaseDate = null;
        bool isTba = true;

        var releaseDatePattern = @"\*\*Release date:\*\*\s*(.+)";
        var match = System.Text.RegularExpressions.Regex.Match(discussion.Body, releaseDatePattern,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (match.Success)
        {
            var dateString = match.Groups[1].Value.Trim();

            if (dateString.Contains("TODO", StringComparison.OrdinalIgnoreCase))
            {
                var todoDatePattern = @"TODO\s*\((\d{4}-\d{2}-\d{2})\)";
                var todoMatch = System.Text.RegularExpressions.Regex.Match(dateString, todoDatePattern);
                if (todoMatch.Success && DateTime.TryParse(todoMatch.Groups[1].Value, out var parsedDate))
                {
                    releaseDate = parsedDate;
                    isTba = true;
                }
            }
            else if (DateTime.TryParse(dateString, out var parsedDate))
            {
                releaseDate = parsedDate;
                isTba = false;
            }
        }

        bool isLts = false;
        var ltsPattern = @"\*\*Long term supported version\*\*\?\s*(Yes|yes)";
        var ltsMatch = System.Text.RegularExpressions.Regex.Match(discussion.Body, ltsPattern);
        if (ltsMatch.Success)
        {
            isLts = true;
        }

        var stats = releaseStats.GetValueOrDefault(releaseLabel, (0, 0, 0));
        var (features, issues, breaking) = stats;

        return new ReleaseDiscussionViewModel
        {
            Version = version,
            ReleaseLabel = releaseLabel,
            ReleaseDate = releaseDate,
            IsReleaseDateTba = isTba,
            IsLts = isLts,
            FeatureCount = features,
            IssueCount = issues,
            BreakingChangesCount = breaking,
            DiscussionUrl = discussion.Url
        };
    }

    private static Dictionary<string, (int features, int issues, int breaking)> CalculateReleaseStats(
        List<Features.GitHubSync.Models.GitHubPullRequest> allPrs,
        List<Features.GitHubSync.Models.GitHubIssue> allIssues)
    {
        var stats = new Dictionary<string, (int features, int issues, int breaking)>();

        foreach (var pr in allPrs)
        {
            foreach (var releaseLabel in pr.Labels.Where(l => l.StartsWith("release/")))
            {
                if (!stats.ContainsKey(releaseLabel))
                    stats[releaseLabel] = (0, 0, 0);

                var current = stats[releaseLabel];

                if (pr.Labels.Any(l => l.Equals("category/feature", StringComparison.OrdinalIgnoreCase) ||
                                       l.Equals("category/notable", StringComparison.OrdinalIgnoreCase)))
                {
                    current.features++;
                }

                if (pr.Labels.Any(l => l.Equals("category/breaking", StringComparison.OrdinalIgnoreCase)))
                {
                    current.breaking++;
                }

                if (pr.Labels.Any(l => l.Equals("category/bugfix", StringComparison.OrdinalIgnoreCase)))
                {
                    current.issues++;
                }

                stats[releaseLabel] = current;
            }
        }

        foreach (var issue in allIssues)
        {
            foreach (var releaseLabel in issue.Labels.Where(l => l.StartsWith("release/")))
            {
                if (!stats.ContainsKey(releaseLabel))
                    stats[releaseLabel] = (0, 0, 0);

                var current = stats[releaseLabel];
                current.issues++;
                stats[releaseLabel] = current;
            }
        }

        return stats;
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

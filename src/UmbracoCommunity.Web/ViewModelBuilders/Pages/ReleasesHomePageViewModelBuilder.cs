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

        public ReleasesHomePageViewModelBuilder(GitHubSqlStore dataStore, Microsoft.Extensions.Options.IOptions<GitHubSyncOptions> options)
        {
            _dataStore = dataStore;
            _options = options.Value;
        }

        public ReleaseDiscussionViewModel? ParseReleaseDiscussion(Features.GitHubSync.Models.GitHubDiscussion discussion,
            Dictionary<string, (int features, int issues, int breaking)> releaseStats)
        {
            // Find the release label (format: "release/X.Y.Z")
            var releaseLabel =
                discussion.Labels.FirstOrDefault(l => l.StartsWith("release/", StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrEmpty(releaseLabel))
                return null;

            // Extract version from label (e.g., "release/16.4.0" -> "16.4.0")
            var version = releaseLabel.Substring("release/".Length);

            // Parse release date from body
            DateTime? releaseDate = null;
            bool isTba = true;

            var releaseDatePattern = @"\*\*Release date:\*\*\s*(.+)";
            var match = System.Text.RegularExpressions.Regex.Match(discussion.Body, releaseDatePattern,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var dateString = match.Groups[1].Value.Trim();

                // Check if it contains "TODO"
                if (dateString.Contains("TODO", StringComparison.OrdinalIgnoreCase))
                {
                    // Try to extract date from "TODO (YYYY-MM-DD)" format
                    var todoDatePattern = @"TODO\s*\((\d{4}-\d{2}-\d{2})\)";
                    var todoMatch = System.Text.RegularExpressions.Regex.Match(dateString, todoDatePattern);
                    if (todoMatch.Success)
                    {
                        if (DateTime.TryParse(todoMatch.Groups[1].Value, out var parsedDate))
                        {
                            releaseDate = parsedDate;
                            isTba = true;
                        }
                    }
                }
                else
                {
                    // Try to parse the date directly (YYYY-MM-DD format)
                    if (DateTime.TryParse(dateString, out var parsedDate))
                    {
                        releaseDate = parsedDate;
                        isTba = false;
                    }
                }
            }
            // If no valid date found, keep isTba = true and releaseDate = null

            // Parse LTS status from body
            bool isLts = false;
            var ltsPattern = @"\*\*Long term supported version\*\*\?\s*(Yes|yes)";
            var ltsMatch = System.Text.RegularExpressions.Regex.Match(discussion.Body, ltsPattern);
            if (ltsMatch.Success)
            {
                isLts = true;
            }

            // Extract description (everything after release date until "### Links")
            var description = string.Empty;
            var bodyLines = discussion.Body.Split('\n');
            bool inDescription = false;
            var descriptionLines = new List<string>();

            foreach (var line in bodyLines)
            {
                if (line.Contains("**Release date:**", StringComparison.OrdinalIgnoreCase))
                {
                    inDescription = true;
                    continue;
                }

                if (inDescription)
                {
                    if (line.TrimStart().StartsWith("### Links", StringComparison.OrdinalIgnoreCase))
                        break;

                    // Skip LTS line
                    if (System.Text.RegularExpressions.Regex.IsMatch(line, @"\*\*Long term supported version\*\*\?"))
                        continue;

                    descriptionLines.Add(line);
                }
            }

            description = string.Join("\n", descriptionLines).Trim();

            // Get stats for this release
            var stats = releaseStats.GetValueOrDefault(releaseLabel, (0, 0, 0));
            var (features, issues, breaking) = stats;

            return new ReleaseDiscussionViewModel
            {
                Version = version,
                ReleaseLabel = releaseLabel,
                ReleaseDate = releaseDate,
                IsReleaseDateTba = isTba,
                IsLts = isLts,
                Description = description,
                FeatureCount = features,
                IssueCount = issues,
                BreakingChangesCount = breaking,
                DiscussionUrl = discussion.Url
            };
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

            // Calculate release stats from PRs and Issues
            var allPrs = _dataStore.GetPullRequestsByLabelPattern(repositoryName, "release/").ToList();
            var allIssues = _dataStore.GetIssuesByLabelPattern(repositoryName, "release/").ToList();

            var releaseStats = CalculateReleaseStats(allPrs, allIssues);

            // Get NuGet package versions from database (use as fallback for missing version info)
            // Find the NuGet package ID for this repository
            var repoConfig = _options.Repositories.FirstOrDefault(r => r.Name.Equals(repositoryName, StringComparison.OrdinalIgnoreCase));

            Dictionary<string, DateTime> nugetVersions = new();
            if (repoConfig?.HasNuGetPackage == true)
            {
                nugetVersions = _dataStore.GetNuGetPackageVersions(repoConfig.NuGetPackageId!);
            }

            // Parse discussions into release view models
            var allReleases = new List<ReleaseDiscussionViewModel>();
            foreach (var discussion in discussions)
            {
                System.Diagnostics.Debug.WriteLine($"Parsing discussion: {discussion.Title}");
                var releaseVm = ParseReleaseDiscussion(discussion, releaseStats);
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
        private static Dictionary<string, (int features, int issues, int breaking)> CalculateReleaseStats(
            List<Features.GitHubSync.Models.GitHubPullRequest> allPrs,
            List<Features.GitHubSync.Models.GitHubIssue> allIssues)
        {
            var stats = new Dictionary<string, (int features, int issues, int breaking)>();

            // Count features, issues, and breaking changes per release label
            foreach (var pr in allPrs)
            {
                foreach (var releaseLabel in pr.Labels.Where(l => l.StartsWith("release/")))
                {
                    if (!stats.ContainsKey(releaseLabel))
                        stats[releaseLabel] = (0, 0, 0);

                    var current = stats[releaseLabel];

                    // Check for features
                    if (pr.Labels.Any(l => l.Equals("category/feature", StringComparison.OrdinalIgnoreCase) ||
                                           l.Equals("category/notable", StringComparison.OrdinalIgnoreCase)))
                    {
                        current.features++;
                    }

                    // Check for breaking changes
                    if (pr.Labels.Any(l => l.Equals("category/breaking", StringComparison.OrdinalIgnoreCase)))
                    {
                        current.breaking++;
                    }

                    // Check for bugfixes (counted as "issues")
                    if (pr.Labels.Any(l => l.Equals("category/bugfix", StringComparison.OrdinalIgnoreCase)))
                    {
                        current.issues++;
                    }

                    stats[releaseLabel] = current;
                }
            }

            // Count issues
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
            // Extract version from "release/X.Y.Z" format
            var versionString = releaseLabel.Replace("release/", "").Trim();

            // Use ParseSemVer to handle pre-release versions properly
            return ParseSemVer(versionString).Version;
        }
    }
}

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;
using UmbracoCommunity.Web.Features.GitHubSync.Infrastructure;
using UmbracoCommunity.Web.Features.ReleaseOverview.Models;
using UmbracoCommunity.Web.Models.Pages;
using UmbracoCommunity.Web.Utilities;

namespace UmbracoCommunity.Web.ViewModelBuilders.Pages;

internal class ComparePageViewModelBuilder : ViewModelBuilderBase, IViewModelBuilder<ComparePageViewModel>
{
    private readonly IGitHubDataStore _dataStore;
    private readonly GitHubSyncOptions _options;
    private readonly IMemoryCache _memoryCache;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly Utilities.ReleaseDiscussionParser _releaseParser;

    public ComparePageViewModelBuilder(
        IGitHubDataStore dataStore,
        IOptions<GitHubSyncOptions> options,
        IMemoryCache memoryCache,
        IHttpContextAccessor httpContextAccessor,
        Utilities.ReleaseDiscussionParser releaseParser)
    {
        _dataStore = dataStore;
        _options = options.Value;
        _memoryCache = memoryCache;
        _httpContextAccessor = httpContextAccessor;
        _releaseParser = releaseParser;
    }

    public ComparePageViewModel Build(IPublishedContent currentPage, IUmbracoContext umbracoContext)
    {
        var viewModel = new ComparePageViewModel(currentPage);

        // Get query parameters
        var queryString = _httpContextAccessor.HttpContext?.Request.Query;
        var fromVersion = queryString?["from"].ToString();
        var toVersion = queryString?["to"].ToString();
        var labelCheck = queryString?["labelCheck"].ToString();
        viewModel.LabelCheck = !string.IsNullOrEmpty(labelCheck) && labelCheck.Equals("true", StringComparison.OrdinalIgnoreCase);

        var includePreReleases = queryString?["includePreReleases"].ToString();
        viewModel.IncludePreReleases = !string.IsNullOrEmpty(includePreReleases) && includePreReleases.Equals("true", StringComparison.OrdinalIgnoreCase);

        var notes = queryString?["notes"].ToString();
        viewModel.ShowNotesOnly = !string.IsNullOrEmpty(notes) && notes.Equals("1", StringComparison.OrdinalIgnoreCase);

        // Get all available versions
        const string repositoryName = "Umbraco-CMS";
        viewModel.AvailableVersions = GetAvailableVersions(repositoryName, viewModel.IncludePreReleases);

        // If both versions are selected, perform the comparison
        if (!string.IsNullOrEmpty(fromVersion) && !string.IsNullOrEmpty(toVersion))
        {
            viewModel.FromVersion = fromVersion;
            viewModel.ToVersion = toVersion;

            // Parse versions to determine lowest and highest
            var fromVer = ParseVersion(fromVersion);
            var toVer = ParseVersion(toVersion);

            // Determine lowest and highest
            Version lowestVer, highestVer;
            if (fromVer <= toVer)
            {
                lowestVer = fromVer;
                highestVer = toVer;
                viewModel.LowestVersion = fromVersion;
                viewModel.HighestVersion = toVersion;
            }
            else
            {
                lowestVer = toVer;
                highestVer = fromVer;
                viewModel.LowestVersion = toVersion;
                viewModel.HighestVersion = fromVersion;
            }

            // Get all available versions in the selected range
            var versionsInRange = viewModel.AvailableVersions
                .Where(v =>
                {
                    var ver = ParseVersion(v.Version);
                    return ver > lowestVer && ver <= highestVer;
                })
                .Select(v => v.Version)
                .ToList();

            // Get all PRs and Issues with release labels
            var allPrs = _dataStore.GetPullRequestsByLabelPattern(repositoryName, "release/").ToList();
            var allIssues = _dataStore.GetIssuesByLabelPattern(repositoryName, "release/").ToList();

            // If this repository has announcements with a prefix, include Announcements repo issues
            var repoConfig = _options.Repositories.FirstOrDefault(r => r.Name.Equals(repositoryName, StringComparison.OrdinalIgnoreCase));
            if (repoConfig?.HasAnnouncementsPrefix == true)
            {
                var prefixedReleaseLabelPattern = $"{repoConfig.AnnouncementsPrefix}/release/";
                var announcementsIssues = _dataStore.GetIssuesByLabelPattern("Announcements", prefixedReleaseLabelPattern).ToList();
                allIssues.AddRange(announcementsIssues);
            }

            // Get first-time contributor PR numbers
            var firstTimeContributorPrNumbers = _dataStore.GetFirstTimeContributorPrNumbers(repositoryName);

            // Initialize version groups with all versions in range
            var versionGroups = new Dictionary<string, List<ReleasePullRequestViewModel>>();
            foreach (var version in versionsInRange)
            {
                versionGroups[version] = new List<ReleasePullRequestViewModel>();
            }

            // Process PRs
            foreach (var pr in allPrs)
            {
                // Find all release labels on this PR that match this repository
                // Include: "release/X.Y.Z" or "{prefix}/release/X.Y.Z" where prefix matches this repo's AnnouncementsPrefix
                var releaseLabels = pr.Labels
                    .Where(l => ReleaseLabelHelper.IsValidReleaseLabelForRepository(l, repoConfig))
                    .ToList();

                // Skip this PR entirely if ANY release label has an invalid SemVer version
                if (releaseLabels.Any(l => !ReleaseLabelHelper.HasValidSemVer(l)))
                    continue;

                // Find the earliest version in our range
                var prVersionsInRange = releaseLabels
                    .Select(label => new { Label = label, Version = ParseVersion(ReleaseLabelHelper.ExtractVersion(label)) })
                    .Where(v => v.Version > lowestVer && v.Version <= highestVer)
                    .OrderBy(v => v.Version)
                    .ToList();

                if (prVersionsInRange.Any())
                {
                    var earliestVersion = prVersionsInRange.First();
                    var versionString = ReleaseLabelHelper.ExtractVersion(earliestVersion.Label);

                    // Skip this PR if the version is not in our allowed list (e.g., filtered out RC versions)
                    if (!versionsInRange.Contains(versionString))
                        continue;

                    var isHqMember = pr.Author != null && _dataStore.IsHqMemberAtTime(pr.Author.Login, pr.CreatedAt);
                    var authorLogin = pr.Author?.Login ?? "unknown";
                    var isFirstTimeContributor = !isHqMember &&
                                                 pr.Author != null &&
                                                 firstTimeContributorPrNumbers.TryGetValue(pr.Author.Login,
                                                     out var firstPrNumber) &&
                                                 pr.Number == firstPrNumber;

                    if (!versionGroups.ContainsKey(versionString))
                    {
                        versionGroups[versionString] = new List<ReleasePullRequestViewModel>();
                    }

                    versionGroups[versionString].Add(new ReleasePullRequestViewModel
                    {
                        Number = pr.Number,
                        Title = pr.Title,
                        Url = pr.Url,
                        AuthorLogin = authorLogin,
                        AuthorName = pr.Author?.Name,
                        AuthorUrl = pr.Author?.Url,
                        AvatarUrl = !isHqMember && authorLogin != "unknown" ? $"https://github.com/{authorLogin}.png" : null,
                        CreatedAt = pr.CreatedAt,
                        Labels = pr.Labels,
                        IsHqMember = isHqMember,
                        IsFirstTimeContributor = isFirstTimeContributor,
                        MergedByLogin = pr.MergedBy?.Login,
                        MergedByName = pr.MergedBy?.Name,
                        MergedByUrl = pr.MergedBy?.Url,
                        State = pr.State
                    });
                }
            }

            // Process Issues
            foreach (var issue in allIssues)
            {
                // Find all release labels on this issue that match this repository
                // Include: "release/X.Y.Z" or "{prefix}/release/X.Y.Z" where prefix matches this repo's AnnouncementsPrefix
                var releaseLabels = issue.Labels
                    .Where(l => ReleaseLabelHelper.IsValidReleaseLabelForRepository(l, repoConfig))
                    .ToList();

                // Skip this issue entirely if ANY release label has an invalid SemVer version
                if (releaseLabels.Any(l => !ReleaseLabelHelper.HasValidSemVer(l)))
                    continue;

                // Find the earliest version in our range
                var issueVersionsInRange = releaseLabels
                    .Select(label => new { Label = label, Version = ParseVersion(ReleaseLabelHelper.ExtractVersion(label)) })
                    .Where(v => v.Version > lowestVer && v.Version <= highestVer)
                    .OrderBy(v => v.Version)
                    .ToList();

                if (issueVersionsInRange.Any())
                {
                    var earliestVersion = issueVersionsInRange.First();
                    var versionString = ReleaseLabelHelper.ExtractVersion(earliestVersion.Label);

                    // Skip this issue if the version is not in our allowed list (e.g., filtered out RC versions)
                    if (!versionsInRange.Contains(versionString))
                        continue;

                    var isHqMember = issue.Author != null &&
                                     _dataStore.IsHqMemberAtTime(issue.Author.Login, issue.CreatedAt);
                    var authorLogin = issue.Author?.Login ?? "unknown";

                    if (!versionGroups.ContainsKey(versionString))
                    {
                        versionGroups[versionString] = new List<ReleasePullRequestViewModel>();
                    }

                    versionGroups[versionString].Add(new ReleasePullRequestViewModel
                    {
                        Number = issue.Number,
                        Title = issue.Title,
                        Url = issue.Url,
                        AuthorLogin = authorLogin,
                        AuthorName = issue.Author?.Name,
                        AuthorUrl = issue.Author?.Url,
                        AvatarUrl = !isHqMember && authorLogin != "unknown" ? $"https://github.com/{authorLogin}.png" : null,
                        CreatedAt = issue.CreatedAt,
                        Labels = issue.Labels,
                        IsHqMember = isHqMember,
                        IsFirstTimeContributor = false,
                        MergedByLogin = null,
                        MergedByName = null,
                        MergedByUrl = null,
                        State = issue.State
                    });
                }
            }

            // Create version groups sorted by version (ascending - oldest first)
            viewModel.VersionGroups = versionGroups
                .OrderBy(kvp => kvp.Key, new SemVerComparer())
                .Select(kvp =>
                {
                    var allPrs = kvp.Value.OrderByDescending(pr => pr.CreatedAt).ToList();
                    var features = allPrs
                        .Where(pr => pr.Labels.Any(l => l.Equals("type/feature", StringComparison.OrdinalIgnoreCase) ||
                                                        l.Equals("category/feature", StringComparison.OrdinalIgnoreCase) ||
                                                        l.Equals("category/notable", StringComparison.OrdinalIgnoreCase)))
                        .ToList();
                    var breakingChanges = allPrs
                        .Where(pr => !features.Contains(pr) &&
                                     pr.Labels.Any(l => l.Equals("category/breaking", StringComparison.OrdinalIgnoreCase)))
                        .ToList();
                    var issuesAndTasks = allPrs
                        .Where(pr => !features.Contains(pr) && !breakingChanges.Contains(pr))
                        .ToList();

                    // Get release notes from available versions
                    var releaseInfo = viewModel.AvailableVersions.FirstOrDefault(v => v.Version == kvp.Key);

                    return new VersionChangesGroup
                    {
                        Version = kvp.Key,
                        Description = releaseInfo?.Description ?? string.Empty,
                        DiscussionUrl = releaseInfo?.DiscussionUrl ?? string.Empty,
                        Features = features,
                        BreakingChanges = breakingChanges,
                        IssuesAndTasks = issuesAndTasks
                    };
                })
                .ToList();

            // Calculate total counts
            viewModel.FeatureCount = viewModel.VersionGroups.Sum(vg => vg.Features.Count);
            viewModel.BreakingChangesCount = viewModel.VersionGroups.Sum(vg => vg.BreakingChanges.Count);
            viewModel.IssuesAndTasksCount = viewModel.VersionGroups.Sum(vg => vg.IssuesAndTasks.Count);

            // Create combined contributors view for all releases in the comparison
            var allPullRequests = viewModel.VersionGroups
                .SelectMany(vg => vg.Features.Concat(vg.BreakingChanges).Concat(vg.IssuesAndTasks))
                .ToList();

            if (allPullRequests.Any())
            {
                viewModel.CombinedContributors = new ReleaseGroupViewModel
                {
                    ReleaseLabel = $"after v{viewModel.LowestVersion}, up to (and including) {viewModel.HighestVersion}",
                    RepositoryName = repositoryName,
                    PullRequests = allPullRequests
                };
            }
        }

        return viewModel;
    }

    private List<ReleaseDiscussionViewModel> GetAvailableVersions(string repositoryName, bool includePreReleases)
    {
        // Cache the version list with pre-releases included
        var cacheKey = $"CompareVersions_{repositoryName}_All";
        var allVersions = _memoryCache.GetOrCreate(cacheKey, entry =>
        {
            entry.Priority = CacheItemPriority.Normal;
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6);

            // Get all release discussions
            var discussions = _dataStore.GetDiscussionsByCategory(repositoryName, "releases").ToList();

            // Get all PRs and issues to calculate stats
            var allPrs = _dataStore.GetPullRequestsByLabelPattern(repositoryName, "release/").ToList();
            var allIssues = _dataStore.GetIssuesByLabelPattern(repositoryName, "release/").ToList();

            // Get repository configuration
            var repoConfig = _options.Repositories.FirstOrDefault(r =>
                r.Name.Equals(repositoryName, StringComparison.OrdinalIgnoreCase));

            // If this repository has announcements with a prefix, include Announcements repo issues for stats calculation
            if (repoConfig?.HasAnnouncementsPrefix == true)
            {
                var prefixedReleaseLabelPattern = $"{repoConfig.AnnouncementsPrefix}/release/";
                var announcementsIssues = _dataStore.GetIssuesByLabelPattern("Announcements", prefixedReleaseLabelPattern).ToList();
                allIssues.AddRange(announcementsIssues);
            }

            var releaseStats = CalculateReleaseStats(allPrs, allIssues);

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
            var allReleases = new Dictionary<string, ReleaseDiscussionViewModel>();
            foreach (var discussion in discussions)
            {
                var releaseVm = _releaseParser.ParseReleaseDiscussion(discussion, releaseStats);
                if (releaseVm != null)
                {
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
                    var stats = releaseStats.GetValueOrDefault(releaseLabel, (0, 0, 0));
                    var (features, issues, breaking) = stats;

                    allReleases[nugetVersion.Key] = new ReleaseDiscussionViewModel
                    {
                        Version = nugetVersion.Key,
                        ReleaseLabel = releaseLabel,
                        ReleaseDate = nugetVersion.Value,
                        IsReleaseDateTba = false,
                        IsLts = false,
                        Description = string.Empty,
                        FeatureCount = features,
                        IssueCount = issues,
                        BreakingChangesCount = breaking,
                        DiscussionUrl = string.Empty
                    };
                }
                else
                {
                    // Update release date from NuGet if missing
                    var existingRelease = allReleases[nugetVersion.Key];
                    if (!existingRelease.ReleaseDate.HasValue || existingRelease.IsReleaseDateTba)
                    {
                        existingRelease.ReleaseDate = nugetVersion.Value;
                        existingRelease.IsReleaseDateTba = false;
                    }
                }
            }

            // Add versions from release labels that don't have discussions or NuGet packages
            var allReleaseLabels = releaseStats.Keys
                .Select(label => label.Replace("release/", ""))
                .Where(version => !allReleases.ContainsKey(version))
                .Where(version => SemVerHelper.IsValidSemVer(version)) // Only include valid SemVer versions
                .ToList();

            foreach (var version in allReleaseLabels)
            {
                var releaseLabel = $"release/{version}";
                var stats = releaseStats.GetValueOrDefault(releaseLabel, (0, 0, 0));
                var (features, issues, breaking) = stats;

                allReleases[version] = new ReleaseDiscussionViewModel
                {
                    Version = version,
                    ReleaseLabel = releaseLabel,
                    ReleaseDate = null,
                    IsReleaseDateTba = true,
                    IsLts = false,
                    Description = string.Empty,
                    FeatureCount = features,
                    IssueCount = issues,
                    BreakingChangesCount = breaking,
                    DiscussionUrl = string.Empty
                };
            }

            // Return all versions (including upcoming and pre-releases), sorted by version descending
            return allReleases.Values
                .OrderByDescending(r => r.Version, new SemVerComparer())
                .ToList();
        })!;

        // Filter pre-releases if not included
        if (!includePreReleases)
        {
            return allVersions.Where(r => !r.Version.Contains('-')).ToList();
        }

        return allVersions;
    }

    private class SemVerComparer : IComparer<string>
    {
        public int Compare(string? x, string? y)
        {
            if (x == null && y == null) return 0;
            if (x == null) return -1;
            if (y == null) return 1;

            var xVersion = ParseSemVer(x);
            var yVersion = ParseSemVer(y);

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

    private static Dictionary<string, (int features, int issues, int breaking)> CalculateReleaseStats(
        List<Features.GitHubSync.Models.GitHubPullRequest> allPrs,
        List<Features.GitHubSync.Models.GitHubIssue> allIssues)
    {
        var stats = new Dictionary<string, (int features, int issues, int breaking)>();

        foreach (var pr in allPrs)
        {
            // Match both "release/" and "{prefix}/release/" patterns (e.g., "cms/release/17.0.0")
            var allReleaseLabels = pr.Labels.Where(l => ReleaseLabelHelper.IsReleaseLabel(l)).ToList();

            // Skip this PR entirely if ANY release label has an invalid SemVer version
            if (allReleaseLabels.Any(l => !ReleaseLabelHelper.HasValidSemVer(l)))
                continue;

            foreach (var releaseLabel in allReleaseLabels)
            {
                var normalizedLabel = ReleaseLabelHelper.Normalize(releaseLabel);

                if (!stats.ContainsKey(normalizedLabel))
                    stats[normalizedLabel] = (0, 0, 0);

                var current = stats[normalizedLabel];

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

                stats[normalizedLabel] = current;
            }
        }

        foreach (var issue in allIssues)
        {
            // Match both "release/" and "{prefix}/release/" patterns (e.g., "cms/release/17.0.0")
            var allReleaseLabels = issue.Labels.Where(l => ReleaseLabelHelper.IsReleaseLabel(l)).ToList();

            // Skip this issue entirely if ANY release label has an invalid SemVer version
            if (allReleaseLabels.Any(l => !ReleaseLabelHelper.HasValidSemVer(l)))
                continue;

            foreach (var releaseLabel in allReleaseLabels)
            {
                var normalizedLabel = ReleaseLabelHelper.Normalize(releaseLabel);

                if (!stats.ContainsKey(normalizedLabel))
                    stats[normalizedLabel] = (0, 0, 0);

                var current = stats[normalizedLabel];

                // Issues with category/breaking label are counted as breaking changes
                if (issue.Labels.Any(l => l.Equals("category/breaking", StringComparison.OrdinalIgnoreCase)))
                {
                    current.breaking++;
                }
                else
                {
                    current.issues++;
                }

                stats[normalizedLabel] = current;
            }
        }

        return stats;
    }

    private static Version ParseVersion(string versionString)
    {
        versionString = versionString.Trim();

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

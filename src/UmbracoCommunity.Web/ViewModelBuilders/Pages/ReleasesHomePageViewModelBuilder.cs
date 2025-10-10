using Microsoft.AspNetCore.Http;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;
using UmbracoCommunity.Web.Features.GitHubSync.Infrastructure;
using UmbracoCommunity.Web.Models.Pages;
using UmbracoCommunity.Web.Models.ReleaseOverview;

namespace UmbracoCommunity.Web.ViewModelBuilders.Pages
{
    internal class ReleasesHomePageViewModelBuilder : ViewModelBuilderBase, IViewModelBuilder<ReleasesHomePageViewModel>
    {
        private readonly GitHubCosmosDbStore _dataStore;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ReleasesHomePageViewModelBuilder(GitHubCosmosDbStore dataStore, IHttpContextAccessor httpContextAccessor)
        {
            _dataStore = dataStore;
            _httpContextAccessor = httpContextAccessor;
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

            var queryString = _httpContextAccessor.HttpContext?.Request.Query;
            var repo = queryString?["repo"].ToString();
            var selectedRelease = queryString?["release"].ToString();
            var compareRelease1 = queryString?["release1"].ToString();
            var compareRelease2 = queryString?["release2"].ToString();

            System.Diagnostics.Debug.WriteLine($"ReleasesViewModelBuilder.Build called. Repo: '{repo}'");

            if (string.IsNullOrEmpty(repo))
            {
                // When no repo is selected, show upcoming releases timeline
                // Use Umbraco-CMS as the default repository
                var defaultRepo = "Umbraco-CMS";
                System.Diagnostics.Debug.WriteLine($"No repo selected, using default: {defaultRepo}");
                PopulateReleaseTimeline(viewModel, defaultRepo);
                System.Diagnostics.Debug.WriteLine(
                    $"After PopulateReleaseTimeline: UpcomingReleases={viewModel.UpcomingReleases.Count}, LatestRelease={viewModel.LatestRelease != null}");
                return viewModel;
            }

            viewModel.SelectedRepo = repo;
            viewModel.SelectedRelease = selectedRelease;
            viewModel.CompareRelease1 = compareRelease1;
            viewModel.CompareRelease2 = compareRelease2;

            // If a specific release is selected, only query for that release
            string labelPattern = "release/";
            if (!string.IsNullOrEmpty(selectedRelease))
            {
                labelPattern = selectedRelease;
            }
            else if (!string.IsNullOrEmpty(compareRelease1) && !string.IsNullOrEmpty(compareRelease2))
            {
                // For comparison, we still need all releases (handled below)
                labelPattern = "release/";
            }

            // Get PRs and Issues with release labels
            var allPrs = _dataStore.GetPullRequestsByLabelPattern(repo, labelPattern).ToList();
            var allIssues = _dataStore.GetIssuesByLabelPattern(repo, labelPattern).ToList();

            // Get first-time contributor PR numbers (maps login to their first PR number)
            var firstTimeContributorPrNumbers = _dataStore.GetFirstTimeContributorPrNumbers(repo);

            // Group by release labels
            var releaseGroups = new Dictionary<string, List<ReleasePullRequestViewModel>>();

            // Process PRs
            foreach (var pr in allPrs)
            {
                // Find all release labels on this PR
                var releaseLabels = pr.Labels.Where(l => l.StartsWith("release/")).ToList();

                foreach (var releaseLabel in releaseLabels)
                {
                    if (!releaseGroups.ContainsKey(releaseLabel))
                    {
                        releaseGroups[releaseLabel] = new List<ReleasePullRequestViewModel>();
                    }

                    var isHqMember = pr.Author != null && _dataStore.IsHqMemberAtTime(pr.Author.Login, pr.CreatedAt);
                    var authorLogin = pr.Author?.Login ?? "unknown";
                    // Only mark as first-time contributor if THIS specific PR is their first one
                    var isFirstTimeContributor = !isHqMember &&
                                                 pr.Author != null &&
                                                 firstTimeContributorPrNumbers.TryGetValue(pr.Author.Login,
                                                     out var firstPrNumber) &&
                                                 pr.Number == firstPrNumber;

                    releaseGroups[releaseLabel].Add(new ReleasePullRequestViewModel
                    {
                        Number = pr.Number,
                        Title = pr.Title,
                        Url = pr.Url,
                        AuthorLogin = authorLogin,
                        AuthorName = pr.Author?.Name,
                        AuthorUrl = pr.Author?.Url,
                        AvatarUrl = authorLogin != "unknown" ? $"https://github.com/{authorLogin}.png" : null,
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
                // Find all release labels on this issue
                var releaseLabels = issue.Labels.Where(l => l.StartsWith("release/")).ToList();

                foreach (var releaseLabel in releaseLabels)
                {
                    if (!releaseGroups.ContainsKey(releaseLabel))
                    {
                        releaseGroups[releaseLabel] = new List<ReleasePullRequestViewModel>();
                    }

                    var isHqMember = issue.Author != null &&
                                     _dataStore.IsHqMemberAtTime(issue.Author.Login, issue.CreatedAt);
                    var authorLogin = issue.Author?.Login ?? "unknown";

                    releaseGroups[releaseLabel].Add(new ReleasePullRequestViewModel
                    {
                        Number = issue.Number,
                        Title = issue.Title,
                        Url = issue.Url,
                        AuthorLogin = authorLogin,
                        AuthorName = issue.Author?.Name,
                        AuthorUrl = issue.Author?.Url,
                        AvatarUrl = authorLogin != "unknown" ? $"https://github.com/{authorLogin}.png" : null,
                        CreatedAt = issue.CreatedAt,
                        Labels = issue.Labels,
                        IsHqMember = isHqMember,
                        IsFirstTimeContributor = false, // Issues don't count for first-time contributors
                        MergedByLogin = null,
                        MergedByName = null,
                        MergedByUrl = null,
                        State = issue.State
                    });
                }
            }

            // Convert to ReleaseGroupViewModel and sort by semver
            var releases = releaseGroups.Select(kvp =>
            {
                var orderedPrs = kvp.Value.OrderByDescending(pr => pr.CreatedAt).ToList();
                var categories = CategorizePullRequests(orderedPrs);

                return new ReleaseGroupViewModel
                {
                    ReleaseLabel = kvp.Key,
                    RepositoryName = repo,
                    PullRequests = orderedPrs,
                    Categories = categories
                };
            }).OrderByDescending(r => ParseVersion(r.ReleaseLabel)).ToList();

            // Filter based on selected release or comparison
            if (!string.IsNullOrEmpty(selectedRelease))
            {
                releases = releases.Where(r => r.ReleaseLabel == selectedRelease).ToList();

                // Populate release info for the selected release
                PopulateReleaseInfo(viewModel, repo, selectedRelease);
            }
            else if (!string.IsNullOrEmpty(compareRelease1) && !string.IsNullOrEmpty(compareRelease2))
            {
                releases = releases.Where(r => r.ReleaseLabel == compareRelease1 || r.ReleaseLabel == compareRelease2)
                    .ToList();
            }

            viewModel.Releases = releases;
            viewModel.AvailableReleases = releaseGroups.Keys.OrderByDescending(ParseVersion).ToList();

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
                    allReleases.Add(releaseVm);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"  Failed to parse (returned null)");
                }
            }

            // Sort by version (descending)
            allReleases = allReleases.OrderByDescending(r => ParseVersion(r.ReleaseLabel)).ToList();

            // Separate upcoming and released
            var now = DateTime.UtcNow;
            viewModel.UpcomingReleases = allReleases
                .Where(r => !r.ReleaseDate.HasValue || r.ReleaseDate.Value > now || r.IsReleaseDateTba)
                .ToList();

            // Find latest release (highest version with past release date)
            viewModel.LatestRelease = allReleases
                .Where(r => r.ReleaseDate.HasValue && r.ReleaseDate.Value <= now && !r.IsReleaseDateTba)
                .OrderByDescending(r => ParseVersion(r.ReleaseLabel))
                .FirstOrDefault();
        }

        private void PopulateReleaseInfo(ReleasesHomePageViewModel viewModel, string repositoryName, string releaseLabel)
        {
            // Get all discussions for the "releases" category
            var discussions = _dataStore.GetDiscussionsByCategory(repositoryName, "releases").ToList();

            // Calculate release stats
            var allPrs = _dataStore.GetPullRequestsByLabelPattern(repositoryName, "release/").ToList();
            var allIssues = _dataStore.GetIssuesByLabelPattern(repositoryName, "release/").ToList();
            var releaseStats = CalculateReleaseStats(allPrs, allIssues);

            // Find the matching discussion
            foreach (var discussion in discussions)
            {
                var releaseVm = ParseReleaseDiscussion(discussion, releaseStats);
                if (releaseVm != null && releaseVm.ReleaseLabel == releaseLabel)
                {
                    viewModel.ReleaseInfo = releaseVm;
                    break;
                }
            }
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

            // Try to parse as Version (handles X.Y.Z format)
            if (Version.TryParse(versionString, out var version))
            {
                return version;
            }

            // Fallback: return a minimal version for unparseable strings
            return new Version(0, 0, 0);
        }

        private static List<ReleaseCategoryViewModel> CategorizePullRequests(
            List<ReleasePullRequestViewModel> pullRequests)
        {
            var categories = new List<ReleaseCategoryViewModel>();

            // Define categories in priority order
            var categoryDefinitions = new[]
            {
                new { Name = "Notable features", Labels = new[] { "category/notable" } },
                new { Name = "Breaking changes", Labels = new[] { "category/breaking" } },
                new { Name = "Developer experience", Labels = new[] { "category/dx" } },
                new { Name = "UI and UX updates", Labels = new[] { "category/ui", "category/ux" } },
                new
                {
                    Name = "API and API documentation updates",
                    Labels = new[] { "category/api", "category/api-documentation" }
                },
                new { Name = "Other features", Labels = new[] { "category/feature" } },
                new { Name = "Bugfixes", Labels = new[] { "category/bugfix" } }
            };

            // Track which PRs have been categorized
            var categorizedPrs = new HashSet<ReleasePullRequestViewModel>();

            // Categorize PRs
            foreach (var categoryDef in categoryDefinitions)
            {
                var categoryPrs = pullRequests
                    .Where(pr => !categorizedPrs.Contains(pr) &&
                                 pr.Labels.Any(label => categoryDef.Labels.Any(catLabel =>
                                     label.Equals(catLabel, StringComparison.OrdinalIgnoreCase))))
                    .ToList();

                if (categoryPrs.Any())
                {
                    categories.Add(new ReleaseCategoryViewModel
                    {
                        CategoryName = categoryDef.Name,
                        PullRequests = categoryPrs
                    });

                    foreach (var pr in categoryPrs)
                    {
                        categorizedPrs.Add(pr);
                    }
                }
            }

            // Add uncategorized PRs to "Other" category
            var uncategorized = pullRequests.Where(pr => !categorizedPrs.Contains(pr)).ToList();
            if (uncategorized.Any())
            {
                categories.Add(new ReleaseCategoryViewModel
                {
                    CategoryName = "Other",
                    PullRequests = uncategorized
                });
            }

            return categories;
        }
    }
}
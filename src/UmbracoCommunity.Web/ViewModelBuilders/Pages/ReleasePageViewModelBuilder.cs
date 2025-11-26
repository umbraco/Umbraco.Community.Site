using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;
using UmbracoCommunity.Web.Features.GitHubSync.Infrastructure;
using UmbracoCommunity.Web.Features.ReleaseOverview.Models;
using UmbracoCommunity.Web.Models.Pages;
using UmbracoCommunity.Web.Utilities;

namespace UmbracoCommunity.Web.ViewModelBuilders.Pages
{
    public class ReleasePageViewModelBuilder : ViewModelBuilderBase, IViewModelBuilder<ReleasePageViewModel>
    {
        private readonly GitHubSqlStore _dataStore;
        private readonly GitHubSyncOptions _options;
        private readonly Utilities.ReleaseDiscussionParser _releaseParser;

        public ReleasePageViewModelBuilder(
            GitHubSqlStore dataStore,
            Microsoft.Extensions.Options.IOptions<GitHubSyncOptions> options,
            Utilities.ReleaseDiscussionParser releaseParser)
        {
            _dataStore = dataStore;
            _options = options.Value;
            _releaseParser = releaseParser;
        }

        public ReleasePageViewModel Build(IPublishedContent currentPage, IUmbracoContext umbracoContext)
        {
            var viewModel = new ReleasePageViewModel(currentPage);
            return viewModel;
        }

        public ReleasePageViewModel Build(
            IPublishedContent currentPage,
            IUmbracoContext umbracoContext,
            string organization,
            string repository,
            string version)
        {
            var viewModel = new ReleasePageViewModel(currentPage)
            {
                Organization = organization,
                Repository = repository,
                Version = version,
                ReleaseLabel = $"release/{version}",
                IsPreRelease = SemVerHelper.IsPreRelease(version),
                StableVersion = SemVerHelper.GetStableVersion(version)
            };

            // Get repository configuration
            var repoConfig = _options.Repositories.FirstOrDefault(r => r.Name.Equals(repository, StringComparison.OrdinalIgnoreCase));
            viewModel.NuGetPackageId = repoConfig?.GetNuGetPackageIds().FirstOrDefault();

            // Get PRs and Issues with this specific release label
            var allPrs = _dataStore.GetPullRequestsByLabelPattern(repository, viewModel.ReleaseLabel).ToList();
            var allIssues = _dataStore.GetIssuesByLabelPattern(repository, viewModel.ReleaseLabel).ToList();

            // If this repository has announcements with a prefix, fetch issues from Announcements repo
            // These will be categorized as Breaking Changes
            if (repoConfig?.HasAnnouncementsPrefix == true)
            {
                var prefixedReleaseLabel = $"{repoConfig.AnnouncementsPrefix}/{viewModel.ReleaseLabel}";
                var announcementsIssues = _dataStore.GetIssuesByLabelPattern("Announcements", prefixedReleaseLabel).ToList();
                allIssues.AddRange(announcementsIssues);
            }

            // Get first-time contributor PR numbers
            var firstTimeContributorPrNumbers = _dataStore.GetFirstTimeContributorPrNumbers(repository);

            // Group by release labels
            var releaseGroups = new Dictionary<string, List<ReleasePullRequestViewModel>>();

            // Process PRs
            foreach (var pr in allPrs)
            {
                // Find all release labels that match this repository
                // Include: "release/X.Y.Z" or "{prefix}/release/X.Y.Z" where prefix matches this repo's AnnouncementsPrefix
                var releaseLabels = pr.Labels
                    .Where(l => ReleaseLabelHelper.IsValidReleaseLabelForRepository(l, repoConfig))
                    .ToList();

                // Skip this PR entirely if ANY release label has an invalid SemVer version
                if (releaseLabels.Any(l => !ReleaseLabelHelper.HasValidSemVer(l)))
                    continue;

                foreach (var releaseLabel in releaseLabels)
                {
                    // Normalize label to remove prefix (e.g., "cms/release/17.0.0" -> "release/17.0.0")
                    var normalizedLabel = ReleaseLabelHelper.Normalize(releaseLabel);

                    if (!releaseGroups.ContainsKey(normalizedLabel))
                    {
                        releaseGroups[normalizedLabel] = new List<ReleasePullRequestViewModel>();
                    }

                    var isHqMember = pr.Author != null && _dataStore.IsHqMemberAtTime(pr.Author.Login, pr.CreatedAt);
                    var authorLogin = pr.Author?.Login ?? "unknown";
                    var isFirstTimeContributor = !isHqMember &&
                                                 pr.Author != null &&
                                                 firstTimeContributorPrNumbers.TryGetValue(pr.Author.Login, out var firstPrNumber) &&
                                                 pr.Number == firstPrNumber;

                    releaseGroups[normalizedLabel].Add(new ReleasePullRequestViewModel
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
                // Find all release labels that match this repository
                // Include: "release/X.Y.Z" or "{prefix}/release/X.Y.Z" where prefix matches this repo's AnnouncementsPrefix
                var releaseLabels = issue.Labels
                    .Where(l => ReleaseLabelHelper.IsValidReleaseLabelForRepository(l, repoConfig))
                    .ToList();

                // Skip this issue entirely if ANY release label has an invalid SemVer version
                if (releaseLabels.Any(l => !ReleaseLabelHelper.HasValidSemVer(l)))
                    continue;

                foreach (var releaseLabel in releaseLabels)
                {
                    // Normalize label to remove prefix (e.g., "cms/release/17.0.0" -> "release/17.0.0")
                    var normalizedLabel = ReleaseLabelHelper.Normalize(releaseLabel);

                    if (!releaseGroups.ContainsKey(normalizedLabel))
                    {
                        releaseGroups[normalizedLabel] = new List<ReleasePullRequestViewModel>();
                    }

                    var isHqMember = issue.Author != null && _dataStore.IsHqMemberAtTime(issue.Author.Login, issue.CreatedAt);
                    var authorLogin = issue.Author?.Login ?? "unknown";

                    releaseGroups[normalizedLabel].Add(new ReleasePullRequestViewModel
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

            // Convert to ReleaseGroupViewModel and categorize
            // For single release page, only include the matching release group
            if (releaseGroups.TryGetValue(viewModel.ReleaseLabel, out var prs))
            {
                var orderedPrs = prs.OrderByDescending(pr => pr.CreatedAt).ToList();
                var categories = CategorizePullRequests(orderedPrs);

                viewModel.Release = new ReleaseGroupViewModel
                {
                    ReleaseLabel = $"in v{viewModel.Version}",
                    RepositoryName = repository,
                    PullRequests = orderedPrs,
                    Categories = categories
                };
            }

            // Get release info from discussions
            PopulateReleaseInfo(viewModel, repository, viewModel.ReleaseLabel);

            return viewModel;
        }

        private void PopulateReleaseInfo(ReleasePageViewModel viewModel, string repositoryName, string releaseLabel)
        {
            var discussions = _dataStore.GetDiscussionsByCategory(repositoryName, "releases").ToList();

            var allPrs = _dataStore.GetPullRequestsByLabelPattern(repositoryName, "release/").ToList();
            var allIssues = _dataStore.GetIssuesByLabelPattern(repositoryName, "release/").ToList();

            // If this repository has announcements with a prefix, include Announcements repo issues for stats calculation
            var repoConfig = _options.Repositories.FirstOrDefault(r => r.Name.Equals(repositoryName, StringComparison.OrdinalIgnoreCase));
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
                    foreach (var versionEntry in versions)
                    {
                        // If version doesn't exist or the new date is earlier, update it
                        if (!nugetVersions.TryGetValue(versionEntry.Key, out DateTime value) || versionEntry.Value < value)
                        {
                            value = versionEntry.Value;
                            nugetVersions[versionEntry.Key] = value;
                        }
                    }
                }
            }

            foreach (var discussion in discussions)
            {
                var releaseVm = _releaseParser.ParseReleaseDiscussion(discussion, releaseStats);
                if (releaseVm != null && releaseVm.ReleaseLabel == releaseLabel)
                {
                    if (nugetVersions.TryGetValue(releaseVm.Version, out var nugetPublishedDate))
                    {
                        releaseVm.IsAvailableOnNuGet = true;

                        if (!releaseVm.ReleaseDate.HasValue || releaseVm.IsReleaseDateTba)
                        {
                            releaseVm.ReleaseDate = nugetPublishedDate;
                            releaseVm.IsReleaseDateTba = false;
                        }
                    }

                    viewModel.ReleaseInfo = releaseVm;
                    return;
                }
            }

            // If no discussion found, create a minimal ReleaseInfo
            var version = releaseLabel.Replace("release/", "").Trim();
            var stats = releaseStats.GetValueOrDefault(releaseLabel, (0, 0, 0));
            var (features, issues, breaking) = stats;

            DateTime? releaseDate = null;
            bool isAvailableOnNuGet = false;
            if (nugetVersions.TryGetValue(version, out var publishedDate))
            {
                releaseDate = publishedDate;
                isAvailableOnNuGet = true;
            }

            viewModel.ReleaseInfo = new ReleaseDiscussionViewModel
            {
                Version = version,
                ReleaseLabel = releaseLabel,
                ReleaseDate = releaseDate,
                IsReleaseDateTba = false,
                IsLts = false,
                Description = string.Empty,
                FeatureCount = features,
                IssueCount = issues,
                BreakingChangesCount = breaking,
                DiscussionUrl = string.Empty,
                IsAvailableOnNuGet = isAvailableOnNuGet
            };
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

        private static List<ReleaseCategoryViewModel> CategorizePullRequests(List<ReleasePullRequestViewModel> pullRequests)
        {
            var categories = new List<ReleaseCategoryViewModel>();

            var categoryDefinitions = new[]
            {
                new { Name = "Notable features", Labels = new[] { "category/notable" } },
                new { Name = "Breaking changes", Labels = new[] { "category/breaking" } },
                new { Name = "Developer experience", Labels = new[] { "category/dx" } },
                new { Name = "UI and UX updates", Labels = new[] { "category/ui", "category/ux" } },
                new { Name = "API and API documentation updates", Labels = new[] { "category/api", "category/api-documentation" } },
                new { Name = "Other features", Labels = new[] { "category/feature" } },
                new { Name = "Bugfixes", Labels = new[] { "category/bugfix" } }
            };

            var categorizedPrs = new HashSet<ReleasePullRequestViewModel>();

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

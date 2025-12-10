using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Security;
using UmbracoCommunity.Extensions.Infrastructure;
using UmbracoCommunity.Extensions.Models;
using UmbracoCommunity.Web.Features.GitHubSync.Infrastructure;
using UmbracoCommunity.Web.Features.GitHubSync.Models;

namespace UmbracoCommunity.Extensions.Controllers
{
    [ApiVersion("1.0")]
    [ApiExplorerSettings(GroupName = "UmbracoCommunity.Extensions")]
    public class UmbracoCommunityExtensionsApiController : UmbracoCommunityExtensionsApiControllerBase
    {
        private readonly IBackOfficeSecurityAccessor _backOfficeSecurityAccessor;
        private readonly GitHubSqlStore _dataStore;
        private readonly GitHubSyncOptions _syncOptions;

        public UmbracoCommunityExtensionsApiController(
            IBackOfficeSecurityAccessor backOfficeSecurityAccessor,
            GitHubSqlStore dataStore,
            IOptions<GitHubSyncOptions> syncOptions)
        {
            _backOfficeSecurityAccessor = backOfficeSecurityAccessor;
            _dataStore = dataStore;
            _syncOptions = syncOptions.Value;
        }

        [HttpGet("ping")]
        [ProducesResponseType<string>(StatusCodes.Status200OK)]
        public string Ping() => "Pong";

        [HttpGet("whatsTheTimeMrWolf")]
        [ProducesResponseType(typeof(DateTime), 200)]
        public DateTime WhatsTheTimeMrWolf() => DateTime.Now;

        [HttpGet("whatsMyName")]
        [ProducesResponseType<string>(StatusCodes.Status200OK)]
        public string WhatsMyName()
        {
            // So we can see a long request in the dashboard with a spinning progress wheel
            Thread.Sleep(2000);

            var currentUser = _backOfficeSecurityAccessor.BackOfficeSecurity?.CurrentUser;
            return currentUser?.Name ?? "I have no idea who you are";
        }

        [HttpGet("whoAmI")]
        [ProducesResponseType<IUser>(StatusCodes.Status200OK)]
        public IUser? WhoAmI() => _backOfficeSecurityAccessor.BackOfficeSecurity?.CurrentUser;

        #region HQ Members

        [HttpGet("hqmembers")]
        [ProducesResponseType<IEnumerable<GitHubHqMember>>(StatusCodes.Status200OK)]
        public IActionResult GetHqMembers()
        {
            var members = _dataStore.GetAllHqMembers();
            return Ok(members);
        }

        [HttpGet("hqmembers/{id}")]
        [ProducesResponseType<GitHubHqMember>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public IActionResult GetHqMember(string id)
        {
            var member = _dataStore.GetHqMemberByLogin(id);
            if (member == null)
            {
                return NotFound();
            }
            return Ok(member);
        }

        [HttpPost("hqmembers")]
        [ProducesResponseType<GitHubHqMember>(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult CreateHqMember([FromBody] GitHubHqMember member)
        {
            if (string.IsNullOrWhiteSpace(member.Login))
            {
                return BadRequest("Login is required");
            }

            var existing = _dataStore.GetHqMemberByLogin(member.Login);
            if (existing != null)
            {
                return BadRequest("A member with this login already exists");
            }

            member.Id = member.Login; // Use Login as ID
            _dataStore.UpsertHqMembers(new[] { member });

            // Retrieve the inserted member
            var created = _dataStore.GetHqMemberByLogin(member.Login)!;
            return CreatedAtAction(nameof(GetHqMember), new { id = created.Login }, created);
        }

        [HttpPut("hqmembers/{id}")]
        [ProducesResponseType<GitHubHqMember>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult UpdateHqMember(string id, [FromBody] GitHubHqMember member)
        {
            var existing = _dataStore.GetHqMemberByLogin(id);
            if (existing == null)
            {
                return NotFound();
            }

            if (string.IsNullOrWhiteSpace(member.Login))
            {
                return BadRequest("Login is required");
            }

            // Check if login is being changed to one that already exists
            if (member.Login != id)
            {
                return BadRequest("Cannot change login");
            }

            member.Id = member.Login; // Ensure the ID matches
            _dataStore.UpsertHqMembers(new[] { member });

            var updated = _dataStore.GetHqMemberByLogin(id)!;
            return Ok(updated);
        }

        [HttpDelete("hqmembers/{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public IActionResult DeleteHqMember(string id)
        {
            var deleted = _dataStore.DeleteHqMemberByLogin(id);
            if (!deleted)
            {
                return NotFound();
            }

            return NoContent();
        }

        [HttpPost("hqmembers/import")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult ImportHqMembers([FromBody] List<GitHubHqMember> members)
        {
            if (members == null || members.Count == 0)
            {
                return BadRequest("No members provided");
            }

            var existingMembers = _dataStore.GetAllHqMembers().ToList();
            foreach (var existing in existingMembers)
            {
                _dataStore.DeleteHqMemberByLogin(existing.Login);
            }

            _dataStore.UpsertHqMembers(members);

            return Ok(new { message = "Import successful", imported = members.Count, cleared = existingMembers.Count });
        }

        [HttpPost("import-sample-hq-members")]
        [AllowAnonymous] // Allow for testing - remove in production
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public IActionResult ImportSampleHqMembers()
        {
            try
            {
                // Clear existing HQ members
                var existingMembers = _dataStore.GetAllHqMembers().ToList();
                foreach (var existing in existingMembers)
                {
                    _dataStore.DeleteHqMemberByLogin(existing.Login);
                }

                // Import HQ Members
                var hqMembers = SampleDataGenerator.GenerateHqMembers();
                var hqResult = _dataStore.UpsertHqMembers(hqMembers);

                return Ok(new { message = "HQ Members imported successfully", added = hqResult.Added, updated = hqResult.Updated });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = $"Failed to import HQ Members: {ex.Message}", stackTrace = ex.StackTrace });
            }
        }

        #endregion

        #region GitHub Data Export/Import

        [HttpGet("export-github-data")]
        [ProducesResponseType<GitHubDataExport>(StatusCodes.Status200OK)]
        public IActionResult ExportGitHubData()
        {
            const string repositoryName = "Umbraco-CMS";

            var export = new GitHubDataExport
            {
                Issues = _dataStore.GetAllIssues(repositoryName).ToList(),
                PullRequests = _dataStore.GetAllPullRequests(repositoryName).ToList(),
                Discussions = _dataStore.GetAllDiscussions(repositoryName).ToList(),
                NuGetPackages = new Dictionary<string, Dictionary<string, DateTime>>()
            };

            // Export NuGet packages
            var umbracoRepo = _syncOptions.Repositories.FirstOrDefault(r => r.Name == repositoryName);
            if (umbracoRepo != null)
            {
                foreach (var packageId in umbracoRepo.GetNuGetPackageIds())
                {
                    var versions = _dataStore.GetNuGetPackageVersions(packageId);
                    export.NuGetPackages[packageId] = versions;
                }
            }

            return Ok(export);
        }


        [HttpPost("import-github-data")]
        [ProducesResponseType<SampleDataImportResult>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public IActionResult ImportGitHubData([FromBody] GitHubDataExport data)
        {
            if (data == null)
            {
                return BadRequest("No data provided");
            }

            try
            {
                // Clear existing GitHub data first (but not HQ members)
                _dataStore.ClearGitHubData();

                var result = new SampleDataImportResult();

                // Import Issues
                if (data.Issues != null && data.Issues.Count > 0)
                {
                    var issuesResult = _dataStore.UpsertIssues(data.Issues);
                    result.IssuesAdded = issuesResult.Added;
                    result.IssuesUpdated = issuesResult.Updated;
                }

                // Import Pull Requests
                if (data.PullRequests != null && data.PullRequests.Count > 0)
                {
                    var prsResult = _dataStore.UpsertPullRequests(data.PullRequests);
                    result.PullRequestsAdded = prsResult.Added;
                    result.PullRequestsUpdated = prsResult.Updated;
                }

                // Import Discussions
                if (data.Discussions != null && data.Discussions.Count > 0)
                {
                    var discussionsResult = _dataStore.UpsertDiscussions(data.Discussions);
                    result.DiscussionsAdded = discussionsResult.Added;
                    result.DiscussionsUpdated = discussionsResult.Updated;
                }

                // Import NuGet Package Versions
                if (data.NuGetPackages != null && data.NuGetPackages.Count > 0)
                {
                    foreach (var package in data.NuGetPackages)
                    {
                        var nugetResult = _dataStore.UpsertNuGetPackageVersions(package.Key, package.Value);
                        result.NuGetPackagesAdded += nugetResult.Added;
                        result.NuGetPackagesUpdated += nugetResult.Updated;
                    }
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = $"Failed to import GitHub data: {ex.Message}", stackTrace = ex.StackTrace });
            }
        }
        [HttpPost("import-sample-github-data")]
        [AllowAnonymous] // Allow for testing - remove in production
        [ProducesResponseType<SampleDataImportResult>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public IActionResult ImportSampleGitHubData()
        {
            try
            {
                var result = new SampleDataImportResult();

                // Import Issues
                try
                {
                    var issues = SampleDataGenerator.GenerateIssues();
                    var issuesResult = _dataStore.UpsertIssues(issues);
                    result.IssuesAdded = issuesResult.Added;
                    result.IssuesUpdated = issuesResult.Updated;
                }
                catch (Exception ex)
                {
                    return StatusCode(StatusCodes.Status500InternalServerError, new { message = $"Failed to import Issues: {ex.Message}", stackTrace = ex.StackTrace });
                }

                // Import Pull Requests
                try
                {
                    var pullRequests = SampleDataGenerator.GeneratePullRequests();
                    var prsResult = _dataStore.UpsertPullRequests(pullRequests);
                    result.PullRequestsAdded = prsResult.Added;
                    result.PullRequestsUpdated = prsResult.Updated;
                }
                catch (Exception ex)
                {
                    return StatusCode(StatusCodes.Status500InternalServerError, new { message = $"Failed to import Pull Requests: {ex.Message}", stackTrace = ex.StackTrace });
                }

                // Import Discussions
                try
                {
                    var discussions = SampleDataGenerator.GenerateDiscussions();
                    var discussionsResult = _dataStore.UpsertDiscussions(discussions);
                    result.DiscussionsAdded = discussionsResult.Added;
                    result.DiscussionsUpdated = discussionsResult.Updated;
                }
                catch (Exception ex)
                {
                    return StatusCode(StatusCodes.Status500InternalServerError, new { message = $"Failed to import Discussions: {ex.Message}", stackTrace = ex.StackTrace });
                }

                // Import NuGet Package Versions
                try
                {
                    var nugetPackages = SampleDataGenerator.GenerateNuGetPackageVersions();
                    var nugetTotalAdded = 0;
                    var nugetTotalUpdated = 0;
                    foreach (var package in nugetPackages)
                    {
                        var nugetResult = _dataStore.UpsertNuGetPackageVersions(package.Key, package.Value);
                        nugetTotalAdded += nugetResult.Added;
                        nugetTotalUpdated += nugetResult.Updated;
                    }
                    result.NuGetPackagesAdded = nugetTotalAdded;
                    result.NuGetPackagesUpdated = nugetTotalUpdated;
                }
                catch (Exception ex)
                {
                    return StatusCode(StatusCodes.Status500InternalServerError, new { message = $"Failed to import NuGet Packages: {ex.Message}", stackTrace = ex.StackTrace });
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = $"Unexpected error: {ex.Message}", stackTrace = ex.StackTrace });
            }
        }

        #endregion

        #region Contributions & Releases

        [HttpGet("contributions")]
        [ProducesResponseType<ContributionStats>(StatusCodes.Status200OK)]
        public IActionResult GetContributionStats(
            [FromQuery] DateTime startDate,
            [FromQuery] DateTime endDate)
        {
            const string repositoryName = "Umbraco-CMS";
            var allPRs = _dataStore.GetAllPullRequests(repositoryName).ToList();

            var prsInRange = allPRs
                .Where(pr => pr.MergedAt.HasValue
                    && pr.MergedAt.Value >= startDate
                    && pr.MergedAt.Value <= endDate)
                .ToList();

            var externalPRs = prsInRange
                .Where(pr => pr.Author != null
                    && !string.IsNullOrEmpty(pr.Author.Login)
                    && pr.MergedAt.HasValue
                    && !_dataStore.IsHqMemberAtTime(pr.Author.Login, pr.MergedAt.Value))
                .ToList();

            var contributorGroups = externalPRs
                .GroupBy(pr => pr.Author!.Login)
                .Select(g => new ContributorDetail
                {
                    Login = g.Key,
                    Name = g.First().Author!.Name ?? g.Key,
                    PullRequestCount = g.Count()
                })
                .OrderByDescending(c => c.PullRequestCount)
                .Take(10)
                .ToList();

            return Ok(new ContributionStats
            {
                TotalExternalPullRequests = externalPRs.Count,
                TotalExternalContributors = externalPRs.Select(pr => pr.Author!.Login).Distinct().Count(),
                StartDate = startDate,
                EndDate = endDate,
                TopContributors = contributorGroups
            });
        }

        [HttpGet("releases")]
        [ProducesResponseType<ReleaseSummary>(StatusCodes.Status200OK)]
        public IActionResult GetReleases(
            [FromQuery] DateTime startDate,
            [FromQuery] DateTime endDate)
        {
            const string repositoryName = "Umbraco-CMS";
            var umbracoRepo = _syncOptions.Repositories.FirstOrDefault(r => r.Name == repositoryName);
            if (umbracoRepo == null)
            {
                return Ok(new ReleaseSummary { StartDate = startDate, EndDate = endDate, Releases = new() });
            }

            var nugetVersions = new Dictionary<string, DateTime>();

            foreach (var packageId in umbracoRepo.GetNuGetPackageIds())
            {
                var versions = _dataStore.GetNuGetPackageVersions(packageId);
                foreach (var version in versions)
                {
                    if (version.Value != default)
                    {
                        if (!nugetVersions.ContainsKey(version.Key) ||
                            version.Value < nugetVersions[version.Key])
                        {
                            nugetVersions[version.Key] = version.Value;
                        }
                    }
                }
            }

            var releases = new List<ReleaseInfo>();
            var discussions = _dataStore.GetDiscussionsByCategory(repositoryName, "Releases");

            foreach (var (version, publishedDate) in nugetVersions.OrderByDescending(kvp => kvp.Value))
            {
                if (publishedDate < startDate || publishedDate > endDate)
                {
                    continue;
                }

                var releaseLabel = $"release/{version}";
                var discussion = discussions.FirstOrDefault(d =>
                    d.Labels != null && d.Labels.Any(l => l.Equals(releaseLabel, StringComparison.OrdinalIgnoreCase)));

                var isLts = discussion?.Body?.Contains("LTS", StringComparison.OrdinalIgnoreCase) ?? false;
                var isPreRelease = IsPreReleaseVersion(version);
                var isMajor = !isPreRelease && IsMajorVersion(version);

                var releasePRs = _dataStore.GetPullRequestsByRelease(repositoryName, releaseLabel).ToList();
                var externalPRs = releasePRs
                    .Where(pr => pr.Author != null
                        && !string.IsNullOrEmpty(pr.Author.Login)
                        && pr.MergedAt.HasValue
                        && !_dataStore.IsHqMemberAtTime(pr.Author.Login, pr.MergedAt.Value))
                    .ToList();

                var topContributors = externalPRs
                    .GroupBy(pr => pr.Author!.Login)
                    .Select(g => new ContributorDetail
                    {
                        Login = g.Key,
                        Name = g.First().Author!.Name ?? g.Key,
                        PullRequestCount = g.Count()
                    })
                    .OrderByDescending(c => c.PullRequestCount)
                    .Take(5)
                    .ToList();

                releases.Add(new ReleaseInfo
                {
                    Version = version,
                    ReleaseDate = publishedDate,
                    IsLts = isLts,
                    IsMajor = isMajor,
                    IsPreRelease = isPreRelease,
                    Url = discussion?.Url ?? string.Empty,
                    TotalPullRequests = releasePRs.Count,
                    ExternalPullRequests = externalPRs.Count,
                    ExternalContributors = externalPRs.Select(pr => pr.Author!.Login).Distinct().Count(),
                    TopContributors = topContributors
                });
            }

            return Ok(new ReleaseSummary
            {
                StartDate = startDate,
                EndDate = endDate,
                Releases = releases
            });
        }

        private static bool IsMajorVersion(string version)
        {
            var parts = version.Split('.');
            if (parts.Length >= 2)
            {
                return parts[1] == "0" && parts[2].Split('-')[0] == "0";
            }
            return false;
        }

        private static bool IsPreReleaseVersion(string version)
        {
            return version.Contains('-');
        }

        #endregion
    }

    #region Data Transfer Objects

    public class SampleDataImportResult
    {
        public int HqMembersAdded { get; set; }
        public int HqMembersUpdated { get; set; }
        public int IssuesAdded { get; set; }
        public int IssuesUpdated { get; set; }
        public int PullRequestsAdded { get; set; }
        public int PullRequestsUpdated { get; set; }
        public int DiscussionsAdded { get; set; }
        public int DiscussionsUpdated { get; set; }
        public int NuGetPackagesAdded { get; set; }
        public int NuGetPackagesUpdated { get; set; }

        public int TotalAdded => HqMembersAdded + IssuesAdded + PullRequestsAdded + DiscussionsAdded + NuGetPackagesAdded;
        public int TotalUpdated => HqMembersUpdated + IssuesUpdated + PullRequestsUpdated + DiscussionsUpdated + NuGetPackagesUpdated;
    }

    public class GitHubDataExport
    {
        public List<GitHubIssue> Issues { get; set; } = new();
        public List<GitHubPullRequest> PullRequests { get; set; } = new();
        public List<GitHubDiscussion> Discussions { get; set; } = new();
        public Dictionary<string, Dictionary<string, DateTime>> NuGetPackages { get; set; } = new();
    }

    #endregion
}

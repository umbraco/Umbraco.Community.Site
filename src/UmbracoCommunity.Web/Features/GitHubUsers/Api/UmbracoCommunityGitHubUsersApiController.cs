using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Security;
using UmbracoCommunity.Web.Features.GitHubSync.Infrastructure;
using UmbracoCommunity.Web.Features.GitHubSync.Models;
using Microsoft.Extensions.Options;
using UmbracoCommunity.Web.Features.GitHubUsers.Api.Models;

namespace UmbracoCommunity.Web.Features.GitHubUsers.Api;

[ApiVersion("1.0")]
[ApiExplorerSettings(GroupName = "UmbracoCommunity.GitHubUsers")]
public class UmbracoCommunityGitHubUsersApiController : UmbracoCommunityGitHubUsersApiControllerBase
{
    private readonly IBackOfficeSecurityAccessor _backOfficeSecurityAccessor;
    private readonly GitHubSqlStore _dataStore;
    private readonly GitHubSyncOptions _syncOptions;

    public UmbracoCommunityGitHubUsersApiController(
        IBackOfficeSecurityAccessor backOfficeSecurityAccessor,
        GitHubSqlStore dataStore,
        IOptions<GitHubSyncOptions> syncOptions)
    {
        _backOfficeSecurityAccessor = backOfficeSecurityAccessor;
        _dataStore = dataStore;
        _syncOptions = syncOptions.Value;
    }

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
}
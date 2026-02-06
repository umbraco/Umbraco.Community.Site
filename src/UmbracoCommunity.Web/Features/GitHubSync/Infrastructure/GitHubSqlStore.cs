using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using UmbracoCommunity.Web.Features.GitHubSync.Infrastructure.Entities;
using UmbracoCommunity.Web.Features.GitHubSync.Models;

namespace UmbracoCommunity.Web.Features.GitHubSync.Infrastructure;

public class GitHubSqlStore
{
    private readonly IDbContextFactory<GitHubDbContext> _contextFactory;
    private readonly IMemoryCache _memoryCache;

    public GitHubSqlStore(IDbContextFactory<GitHubDbContext> contextFactory, IMemoryCache memoryCache)
    {
        _contextFactory = contextFactory;
        _memoryCache = memoryCache;
    }

    public GitHubSyncResult UpsertPullRequests(IEnumerable<GitHubPullRequest> pullRequests)
    {
        return UpsertPullRequestsAsync(pullRequests).GetAwaiter().GetResult();
    }

    private async Task<GitHubSyncResult> UpsertPullRequestsAsync(IEnumerable<GitHubPullRequest> pullRequests)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var result = new GitHubSyncResult();
        var repositoryNames = new HashSet<string>();

        foreach (var pr in pullRequests)
        {
            repositoryNames.Add(pr.Repository.Name);

            var entity = await context.PullRequests.FindAsync(pr.Id);
            var json = JsonConvert.SerializeObject(pr);

            if (entity == null)
            {
                entity = new PullRequestEntity
                {
                    Id = pr.Id,
                    RepositoryName = pr.Repository.Name,
                    Number = pr.Number,
                    CreatedAt = pr.CreatedAt,
                    Data = json
                };
                context.PullRequests.Add(entity);
                result.Added++;
            }
            else
            {
                entity.Data = json;
                entity.CreatedAt = pr.CreatedAt;
                result.Updated++;
            }

            // Handle release labels - delete all existing and re-add
            var existingLabels = await context.PullRequestReleases
                .Where(prl => prl.PullRequestId == pr.Id)
                .ToListAsync();
            context.PullRequestReleases.RemoveRange(existingLabels);

            // Match both "release/" and "{prefix}/release/" patterns (e.g., "cms/release/17.0.0")
            var releaseLabels = pr.Labels
                .Where(l => l.StartsWith("release/", StringComparison.OrdinalIgnoreCase) ||
                           l.Contains("/release/", StringComparison.OrdinalIgnoreCase))
                .Select(l => new PullRequestReleaseEntity
                {
                    PullRequestId = pr.Id,
                    ReleaseLabel = l
                });
            await context.PullRequestReleases.AddRangeAsync(releaseLabels);
        }

        await context.SaveChangesAsync();

        // Clear cache for affected repositories
        foreach (var repo in repositoryNames)
        {
            _memoryCache.Remove($"AvailableReleases_{repo}");
            _memoryCache.Remove($"AllPullRequests_{repo}");
            _memoryCache.Remove($"FirstTimeContributors_{repo}");
        }

        return result;
    }

    public GitHubSyncResult UpsertIssues(IEnumerable<GitHubIssue> issues)
    {
        return UpsertIssuesAsync(issues).GetAwaiter().GetResult();
    }

    private async Task<GitHubSyncResult> UpsertIssuesAsync(IEnumerable<GitHubIssue> issues)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var result = new GitHubSyncResult();
        var repositoryNames = new HashSet<string>();

        foreach (var issue in issues)
        {
            repositoryNames.Add(issue.Repository.Name);

            var entity = await context.Issues.FindAsync(issue.Id);
            var json = JsonConvert.SerializeObject(issue);

            if (entity == null)
            {
                entity = new IssueEntity
                {
                    Id = issue.Id,
                    RepositoryName = issue.Repository.Name,
                    Number = issue.Number,
                    CreatedAt = issue.CreatedAt,
                    Data = json
                };
                context.Issues.Add(entity);
                result.Added++;
            }
            else
            {
                entity.Data = json;
                entity.CreatedAt = issue.CreatedAt;
                result.Updated++;
            }

            // Handle release labels - delete all existing and re-add
            var existingLabels = await context.IssueReleases
                .Where(irl => irl.IssueId == issue.Id)
                .ToListAsync();
            context.IssueReleases.RemoveRange(existingLabels);

            // Match both "release/" and "{prefix}/release/" patterns (e.g., "cms/release/17.0.0")
            var releaseLabels = issue.Labels
                .Where(l => l.StartsWith("release/", StringComparison.OrdinalIgnoreCase) ||
                           l.Contains("/release/", StringComparison.OrdinalIgnoreCase))
                .Select(l => new IssueReleaseEntity
                {
                    IssueId = issue.Id,
                    ReleaseLabel = l
                });
            await context.IssueReleases.AddRangeAsync(releaseLabels);
        }

        await context.SaveChangesAsync();

        // Clear cache for affected repositories
        foreach (var repo in repositoryNames)
        {
            _memoryCache.Remove($"AvailableReleases_{repo}");
            _memoryCache.Remove($"AllIssues_{repo}");
        }

        return result;
    }

    public GitHubSyncResult UpsertDiscussions(IEnumerable<GitHubDiscussion> discussions)
    {
        return UpsertDiscussionsAsync(discussions).GetAwaiter().GetResult();
    }

    private async Task<GitHubSyncResult> UpsertDiscussionsAsync(IEnumerable<GitHubDiscussion> discussions)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var result = new GitHubSyncResult();

        foreach (var discussion in discussions)
        {
            var entity = await context.Discussions.FindAsync(discussion.Id);
            var json = JsonConvert.SerializeObject(discussion);

            if (entity == null)
            {
                entity = new DiscussionEntity
                {
                    Id = discussion.Id,
                    RepositoryName = discussion.Repository.Name,
                    Number = discussion.Number,
                    CreatedAt = discussion.CreatedAt,
                    Data = json
                };
                context.Discussions.Add(entity);
                result.Added++;
            }
            else
            {
                entity.Data = json;
                entity.CreatedAt = discussion.CreatedAt;
                result.Updated++;
            }
        }

        await context.SaveChangesAsync();
        return result;
    }

    public GitHubSyncResult UpsertHqMembers(IEnumerable<GitHubHqMember> hqMembers)
    {
        return UpsertHqMembersAsync(hqMembers).GetAwaiter().GetResult();
    }

    private async Task<GitHubSyncResult> UpsertHqMembersAsync(IEnumerable<GitHubHqMember> hqMembers)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var result = new GitHubSyncResult();

        foreach (var member in hqMembers)
        {
            // Look up by Login (which has a unique index) rather than Id
            // This ensures updates work correctly when the Id doesn't match the database primary key
            var entity = await context.HqMembers.FirstOrDefaultAsync(m => m.Login == member.Login);
            var json = JsonConvert.SerializeObject(member);

            if (entity == null)
            {
                entity = new HqMemberEntity
                {
                    Id = member.Id,
                    Login = member.Login,
                    Data = json
                };
                context.HqMembers.Add(entity);
                result.Added++;
            }
            else
            {
                entity.Data = json;
                result.Updated++;
            }
        }

        await context.SaveChangesAsync();
        return result;
    }

    public IEnumerable<GitHubPullRequest> GetAllPullRequests(string repositoryName)
    {
        return GetAllPullRequestsAsync(repositoryName).GetAwaiter().GetResult();
    }

    private async Task<IEnumerable<GitHubPullRequest>> GetAllPullRequestsAsync(string repositoryName)
    {
        // Cache this expensive query to avoid repeated database hits
        var cacheKey = $"AllPullRequests_{repositoryName}";
        var cachedResult = _memoryCache.Get<IEnumerable<GitHubPullRequest>>(cacheKey);
        if (cachedResult != null)
        {
            return cachedResult;
        }

        using var context = await _contextFactory.CreateDbContextAsync();

        // Add limit to prevent memory exhaustion - only get PRs with release labels
        // This matches the pattern used in GetPullRequestsByRelease and significantly reduces memory usage
        var entities = await context.PullRequestReleases
            .AsNoTracking()
            .Select(prl => prl.PullRequestId)
            .Distinct()
            .Join(context.PullRequests,
                prId => prId,
                pr => pr.Id,
                (prId, pr) => pr)
            .Where(pr => pr.RepositoryName == repositoryName)
            .OrderByDescending(pr => pr.CreatedAt)
            .ToListAsync();

        var result = entities.Select(e => JsonConvert.DeserializeObject<GitHubPullRequest>(e.Data)!).ToList();

        // Cache for 1 hour
        _memoryCache.Set(cacheKey, result, TimeSpan.FromHours(1));

        return result;
    }

    public IEnumerable<GitHubPullRequest> GetPullRequestsByRelease(string repositoryName, string releaseLabel)
    {
        return GetPullRequestsByReleaseAsync(repositoryName, releaseLabel).GetAwaiter().GetResult();
    }

    private async Task<IEnumerable<GitHubPullRequest>> GetPullRequestsByReleaseAsync(string repositoryName, string releaseLabel)
    {
        using var context = await _contextFactory.CreateDbContextAsync();

        var entities = await context.PullRequestReleases
            .AsNoTracking()
            .Where(prl => prl.ReleaseLabel == releaseLabel)
            .Join(context.PullRequests,
                prl => prl.PullRequestId,
                pr => pr.Id,
                (prl, pr) => pr)
            .Where(pr => pr.RepositoryName == repositoryName)
            .OrderByDescending(pr => pr.CreatedAt)
            .Take(1000)
            .ToListAsync();

        return entities.Select(e => JsonConvert.DeserializeObject<GitHubPullRequest>(e.Data)!);
    }

    public IEnumerable<GitHubIssue> GetAllIssues(string repositoryName)
    {
        return GetAllIssuesAsync(repositoryName).GetAwaiter().GetResult();
    }

    private async Task<IEnumerable<GitHubIssue>> GetAllIssuesAsync(string repositoryName)
    {
        // Cache this expensive query to avoid repeated database hits
        var cacheKey = $"AllIssues_{repositoryName}";
        var cachedResult = _memoryCache.Get<IEnumerable<GitHubIssue>>(cacheKey);
        if (cachedResult != null)
        {
            return cachedResult;
        }

        using var context = await _contextFactory.CreateDbContextAsync();

        // Add limit to prevent memory exhaustion - only get issues with release labels
        // This matches the pattern used in GetIssuesByRelease and significantly reduces memory usage
        var entities = await context.IssueReleases
            .AsNoTracking()
            .Select(irl => irl.IssueId)
            .Distinct()
            .Join(context.Issues,
                issueId => issueId,
                i => i.Id,
                (issueId, i) => i)
            .Where(i => i.RepositoryName == repositoryName)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();

        var result = entities.Select(e => JsonConvert.DeserializeObject<GitHubIssue>(e.Data)!).ToList();

        // Cache for 1 hour
        _memoryCache.Set(cacheKey, result, TimeSpan.FromHours(1));

        return result;
    }

    public IEnumerable<GitHubIssue> GetIssuesByRelease(string repositoryName, string releaseLabel)
    {
        return GetIssuesByReleaseAsync(repositoryName, releaseLabel).GetAwaiter().GetResult();
    }

    private async Task<IEnumerable<GitHubIssue>> GetIssuesByReleaseAsync(string repositoryName, string releaseLabel)
    {
        using var context = await _contextFactory.CreateDbContextAsync();

        var entities = await context.IssueReleases
            .AsNoTracking()
            .Where(irl => irl.ReleaseLabel == releaseLabel)
            .Join(context.Issues,
                irl => irl.IssueId,
                i => i.Id,
                (irl, i) => i)
            .Where(i => i.RepositoryName == repositoryName)
            .OrderByDescending(i => i.CreatedAt)
            .Take(1000)
            .ToListAsync();

        return entities.Select(e => JsonConvert.DeserializeObject<GitHubIssue>(e.Data)!);
    }

    public IEnumerable<GitHubDiscussion> GetAllDiscussions(string repositoryName)
    {
        return GetAllDiscussionsAsync(repositoryName).GetAwaiter().GetResult();
    }

    private async Task<IEnumerable<GitHubDiscussion>> GetAllDiscussionsAsync(string repositoryName)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var entities = await context.Discussions
            .AsNoTracking()
            .Where(d => d.RepositoryName == repositoryName)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();

        return entities.Select(e => JsonConvert.DeserializeObject<GitHubDiscussion>(e.Data)!);
    }

    public IEnumerable<GitHubHqMember> GetAllHqMembers()
    {
        return GetAllHqMembersAsync().GetAwaiter().GetResult();
    }

    private async Task<IEnumerable<GitHubHqMember>> GetAllHqMembersAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var entities = await context.HqMembers.AsNoTracking().ToListAsync();

        return entities.Select(e => JsonConvert.DeserializeObject<GitHubHqMember>(e.Data)!);
    }

    // Alias for GetAllHqMembers for backward compatibility
    public IEnumerable<GitHubHqMember> GetHqMembers() => GetAllHqMembers();

    public GitHubHqMember? GetHqMemberByLogin(string login)
    {
        return GetHqMemberByLoginAsync(login).GetAwaiter().GetResult();
    }

    private async Task<GitHubHqMember?> GetHqMemberByLoginAsync(string login)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var entity = await context.HqMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Login == login);

        return entity != null ? JsonConvert.DeserializeObject<GitHubHqMember>(entity.Data) : null;
    }

    public bool DeleteHqMemberByLogin(string login)
    {
        return DeleteHqMemberByLoginAsync(login).GetAwaiter().GetResult();
    }

    private async Task<bool> DeleteHqMemberByLoginAsync(string login)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var entity = await context.HqMembers
            .FirstOrDefaultAsync(m => m.Login == login);

        if (entity == null)
            return false;

        context.HqMembers.Remove(entity);
        await context.SaveChangesAsync();
        return true;
    }

    // Backward compatibility methods
    public IEnumerable<GitHubPullRequest> GetPullRequestsByLabelPattern(string repositoryName, string labelPattern)
    {
        // For SQL implementation, we use the specific release label query
        // If labelPattern ends with "release/" (e.g., "release/" or "cms/release/"), return all PRs since we can't efficiently filter by pattern in SQL
        if (labelPattern.EndsWith("release/", StringComparison.OrdinalIgnoreCase))
        {
            return GetAllPullRequests(repositoryName);
        }
        return GetPullRequestsByRelease(repositoryName, labelPattern);
    }

    public IEnumerable<GitHubIssue> GetIssuesByLabelPattern(string repositoryName, string labelPattern)
    {
        // For SQL implementation, we use the specific release label query
        // If labelPattern ends with "release/" (e.g., "release/" or "cms/release/"), return all issues since we can't efficiently filter by pattern in SQL
        if (labelPattern.EndsWith("release/", StringComparison.OrdinalIgnoreCase))
        {
            return GetAllIssues(repositoryName);
        }
        return GetIssuesByRelease(repositoryName, labelPattern);
    }

    public IEnumerable<GitHubDiscussion> GetDiscussionsByCategory(string repositoryName, string category)
    {
        return GetDiscussionsByCategoryAsync(repositoryName, category).GetAwaiter().GetResult();
    }

    private async Task<IEnumerable<GitHubDiscussion>> GetDiscussionsByCategoryAsync(string repositoryName, string category)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var entities = await context.Discussions
            .AsNoTracking()
            .Where(d => d.RepositoryName == repositoryName)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();

        var discussions = entities.Select(e => JsonConvert.DeserializeObject<GitHubDiscussion>(e.Data)!);

        // Filter by category in memory (since category is in JSON)
        return discussions.Where(d => d.CategoryName.Equals(category, StringComparison.OrdinalIgnoreCase));
    }

    public Dictionary<string, int> GetFirstTimeContributorPrNumbers(string repositoryName)
    {
        return GetFirstTimeContributorPrNumbersAsync(repositoryName).GetAwaiter().GetResult();
    }

    private async Task<Dictionary<string, int>> GetFirstTimeContributorPrNumbersAsync(string repositoryName)
    {
        // Cache this expensive query to avoid repeated database hits
        var cacheKey = $"FirstTimeContributors_{repositoryName}";
        var cachedResult = _memoryCache.Get<Dictionary<string, int>>(cacheKey);
        if (cachedResult != null)
        {
            return cachedResult;
        }

        using var context = await _contextFactory.CreateDbContextAsync();

        // Only load PRs that have release labels to reduce memory usage
        var entities = await context.PullRequestReleases
            .AsNoTracking()
            .Select(prl => prl.PullRequestId)
            .Distinct()
            .Join(context.PullRequests,
                prId => prId,
                pr => pr.Id,
                (prId, pr) => pr)
            .Where(pr => pr.RepositoryName == repositoryName)
            .OrderBy(pr => pr.CreatedAt)
            .ToListAsync();

        var prs = entities.Select(e => JsonConvert.DeserializeObject<GitHubPullRequest>(e.Data)!);

        // Find first PR for each author - returns a dictionary mapping login to their first PR number
        var firstPrByAuthor = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var pr in prs)
        {
            if (pr.Author != null && !string.IsNullOrEmpty(pr.Author.Login))
            {
                if (!firstPrByAuthor.ContainsKey(pr.Author.Login))
                {
                    firstPrByAuthor[pr.Author.Login] = pr.Number;
                }
            }
        }

        // Cache for 2 hours (this data changes less frequently)
        _memoryCache.Set(cacheKey, firstPrByAuthor, TimeSpan.FromHours(2));

        return firstPrByAuthor;
    }

    public bool IsHqMemberAtTime(string login, DateTime time)
    {
        return IsHqMemberAtTimeAsync(login, time).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Checks if a login was an HQ member at a given time using a pre-loaded list of HQ members.
    /// Use this method in loops to avoid N+1 database queries.
    /// </summary>
    /// <param name="login">The GitHub login to check</param>
    /// <param name="time">The time to check membership at</param>
    /// <param name="hqMembers">Pre-loaded list of HQ members (call GetAllHqMembers() once and pass here)</param>
    /// <returns>True if the login was an HQ member at the given time</returns>
    public static bool IsHqMemberAtTime(string login, DateTime time, IEnumerable<GitHubHqMember> hqMembers)
    {
        var member = hqMembers.FirstOrDefault(m =>
            m.Login.Equals(login, StringComparison.OrdinalIgnoreCase));

        if (member == null)
            return false;

        // If no periods are defined, treat as always HQ member
        if (member.Periods == null || !member.Periods.Any())
            return true;

        // Check if the time falls within any of the member's HQ periods
        foreach (var period in member.Periods)
        {
            // If Start is null, it means they've been HQ from the beginning
            var startCheck = !period.Start.HasValue || time >= period.Start.Value;
            // If End is null, it means they're still HQ
            var endCheck = !period.End.HasValue || time <= period.End.Value;

            if (startCheck && endCheck)
            {
                return true;
            }
        }

        return false;
    }

    private async Task<bool> IsHqMemberAtTimeAsync(string login, DateTime time)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var entity = await context.HqMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Login == login);

        if (entity == null)
            return false;

        var member = JsonConvert.DeserializeObject<GitHubHqMember>(entity.Data)!;

        // If no periods are defined, treat as always HQ member
        if (member.Periods == null || !member.Periods.Any())
            return true;

        // Check if the time falls within any of the member's HQ periods
        foreach (var period in member.Periods)
        {
            // If Start is null, it means they've been HQ from the beginning
            var startCheck = !period.Start.HasValue || time >= period.Start.Value;
            // If End is null, it means they're still HQ
            var endCheck = !period.End.HasValue || time <= period.End.Value;

            if (startCheck && endCheck)
            {
                return true;
            }
        }

        return false;
    }

    public GitHubSyncResult UpsertNuGetPackageVersions(string packageId, Dictionary<string, DateTime> versions)
    {
        return UpsertNuGetPackageVersionsAsync(packageId, versions).GetAwaiter().GetResult();
    }

    private async Task<GitHubSyncResult> UpsertNuGetPackageVersionsAsync(string packageId, Dictionary<string, DateTime> versions)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var result = new GitHubSyncResult();
        var now = DateTime.UtcNow;

        foreach (var kvp in versions)
        {
            var version = kvp.Key;
            var publishedDate = kvp.Value;

            var entity = await context.NuGetPackageVersions
                .FirstOrDefaultAsync(e => e.PackageId == packageId && e.Version == version);

            if (entity == null)
            {
                entity = new NuGetPackageVersionEntity
                {
                    PackageId = packageId,
                    Version = version,
                    PublishedDate = publishedDate,
                    LastSyncedAt = now
                };
                context.NuGetPackageVersions.Add(entity);
                result.Added++;
            }
            else
            {
                entity.PublishedDate = publishedDate;
                entity.LastSyncedAt = now;
                result.Updated++;
            }
        }

        await context.SaveChangesAsync();

        // Clear cache for NuGet versions
        _memoryCache.Remove($"NuGet_{packageId}_Versions");

        return result;
    }

    public Dictionary<string, DateTime> GetNuGetPackageVersions(string packageId)
    {
        return GetNuGetPackageVersionsAsync(packageId).GetAwaiter().GetResult();
    }

    private async Task<Dictionary<string, DateTime>> GetNuGetPackageVersionsAsync(string packageId)
    {
        using var context = await _contextFactory.CreateDbContextAsync();

        var versions = await context.NuGetPackageVersions
            .AsNoTracking()
            .Where(e => e.PackageId == packageId)
            .ToDictionaryAsync(e => e.Version, e => e.PublishedDate);

        return versions;
    }

    public void ClearAllData()
    {
        ClearAllDataAsync().GetAwaiter().GetResult();
    }

    private async Task ClearAllDataAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();

        // Delete junction tables first (foreign key constraints)
        await context.Database.ExecuteSqlRawAsync("DELETE FROM GitHubPullRequestReleases");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM GitHubIssueReleases");

        // Delete main tables
        await context.Database.ExecuteSqlRawAsync("DELETE FROM GitHubPullRequests");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM GitHubIssues");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM GitHubDiscussions");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM GitHubHqMembers");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM NuGetPackageVersions");

        // Clear all cache
        _memoryCache.Remove("AvailableReleases_Umbraco-CMS");
        _memoryCache.Remove("AvailableReleases_Umbraco.Forms");
        _memoryCache.Remove("AvailableReleases_Umbraco.Deploy");
        _memoryCache.Remove("AvailableReleases_Umbraco.Workflow");
        _memoryCache.Remove("AvailableReleases_Umbraco.Commerce");

        // Clear data store query caches
        _memoryCache.Remove("AllPullRequests_Umbraco-CMS");
        _memoryCache.Remove("AllIssues_Umbraco-CMS");
        _memoryCache.Remove("AllIssues_Announcements");
        _memoryCache.Remove("FirstTimeContributors_Umbraco-CMS");

        // Clear NuGet cache
        _memoryCache.Remove("NuGet_Umbraco.Cms_Versions");
        _memoryCache.Remove("NuGet_Umbraco.Forms_Versions");
        _memoryCache.Remove("NuGet_Umbraco.Deploy_Versions");
        _memoryCache.Remove("NuGet_Umbraco.Workflow_Versions");
        _memoryCache.Remove("NuGet_Umbraco.Commerce_Versions");
    }


    public void ClearGitHubData()
    {
        ClearGitHubDataAsync().GetAwaiter().GetResult();
    }

    private async Task ClearGitHubDataAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();

        // Delete junction tables first (foreign key constraints)
        await context.Database.ExecuteSqlRawAsync("DELETE FROM GitHubPullRequestReleases");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM GitHubIssueReleases");

        // Delete main tables (but not HQ members)
        await context.Database.ExecuteSqlRawAsync("DELETE FROM GitHubPullRequests");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM GitHubIssues");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM GitHubDiscussions");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM NuGetPackageVersions");

        // Clear release cache
        _memoryCache.Remove("AvailableReleases_Umbraco-CMS");
        _memoryCache.Remove("AvailableReleases_Umbraco.Forms");
        _memoryCache.Remove("AvailableReleases_Umbraco.Deploy");
        _memoryCache.Remove("AvailableReleases_Umbraco.Workflow");
        _memoryCache.Remove("AvailableReleases_Umbraco.Commerce");

        // Clear data store query caches
        _memoryCache.Remove("AllPullRequests_Umbraco-CMS");
        _memoryCache.Remove("AllIssues_Umbraco-CMS");
        _memoryCache.Remove("AllIssues_Announcements");
        _memoryCache.Remove("FirstTimeContributors_Umbraco-CMS");

        // Clear NuGet cache
        _memoryCache.Remove("NuGet_Umbraco.Cms_Versions");
        _memoryCache.Remove("NuGet_Umbraco.Forms_Versions");
        _memoryCache.Remove("NuGet_Umbraco.Deploy_Versions");
        _memoryCache.Remove("NuGet_Umbraco.Workflow_Versions");
        _memoryCache.Remove("NuGet_Umbraco.Commerce_Versions");
    }
}

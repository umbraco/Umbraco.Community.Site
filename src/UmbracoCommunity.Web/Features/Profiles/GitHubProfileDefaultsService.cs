using Microsoft.Extensions.Logging;
using UmbracoCommunity.Web.Features.Profiles.Data;
using UmbracoCommunity.Web.Features.Profiles.Data.Entities;

namespace UmbracoCommunity.Web.Features.Profiles;

/// <summary>
/// Fills in a profile's cached GitHub bio and company (the defaults shown when the member
/// hasn't written their own bio or headline). They are fetched once and stored on the profile
/// row; a failed fetch is not stored, so it is tried again on the next call. Only throws when cancelled.
/// </summary>
public class GitHubProfileDefaultsService
{
    private readonly GitHubProfileClient _client;
    private readonly MemberProfileStore _store;
    private readonly ILogger<GitHubProfileDefaultsService> _logger;

    public GitHubProfileDefaultsService(GitHubProfileClient client, MemberProfileStore store, ILogger<GitHubProfileDefaultsService> logger)
    {
        _client = client;
        _store = store;
        _logger = logger;
    }

    /// <summary>
    /// Fetches and stores the GitHub bio and company when they have not been fetched yet,
    /// updating <paramref name="profile"/> in place. Does nothing when they have already been fetched.
    /// </summary>
    public async Task EnsureGitHubDefaultsAsync(MemberProfileEntity profile, CancellationToken cancellationToken = default)
    {
        if (profile.GitHubFetchedUtc.HasValue)
        {
            return;
        }

        try
        {
            var result = await _client.GetProfileAsync(profile.GitHubHandle, cancellationToken);
            if (!result.Succeeded)
            {
                return;
            }

            var fetchedUtc = DateTime.UtcNow;
            await _store.SetGitHubDefaultsAsync(profile.MemberKey, result.Bio, result.Company, fetchedUtc, cancellationToken);
            profile.GitHubBio = result.Bio;
            profile.GitHubCompany = result.Company;
            profile.GitHubFetchedUtc = fetchedUtc;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Couldn't store the GitHub profile defaults for {Handle}", profile.GitHubHandle);
        }
    }
}

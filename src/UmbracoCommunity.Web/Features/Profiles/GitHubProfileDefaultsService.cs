using Microsoft.Extensions.Logging;
using UmbracoCommunity.Web.Features.Profiles.Data;
using UmbracoCommunity.Web.Features.Profiles.Data.Entities;

namespace UmbracoCommunity.Web.Features.Profiles;

/// <summary>
/// One-time catch-up for completed profiles whose GitHub bio and company were never fetched (the GitHub call failed at
/// onboarding, or the profile predates automatic onboarding): fetches them once, caches them on the row and copies them
/// into the member's own <see cref="MemberProfileEntity.Headline"/> and <see cref="MemberProfileEntity.Bio"/> when those
/// are still empty, exactly as <see cref="ProfileAutoOnboardingService"/> does at onboarding. The GitHub values are never
/// shown as a fallback. A failed fetch is not stored, so it is tried again on the next call. Only throws when cancelled.
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
    /// When the GitHub bio and company have not been fetched yet, fetches and stores them and fills the member's own
    /// headline and bio from them where those are still null, updating <paramref name="profile"/> in place. Does nothing
    /// once they have been fetched, so text the member clears later is never filled again.
    /// </summary>
    public async Task BackfillFromGitHubOnceAsync(MemberProfileEntity profile, CancellationToken cancellationToken = default)
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

            // Fill first, then mark as fetched, so a failure in between leads to another try rather than a skipped fill.
            var filled = await _store.FillMissingProfileTextAsync(
                profile.MemberKey,
                displayName: null,
                ProfileText.NormalizeAndTruncate(result.Company, ProfileText.MaxHeadlineLength),
                ProfileText.NormalizeAndTruncate(result.Bio, ProfileText.MaxBioLength),
                cancellationToken);
            if (filled != null)
            {
                profile.Headline = filled.Headline;
                profile.Bio = filled.Bio;
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
            _logger.LogWarning(ex, "Couldn't fill the profile of {Handle} from GitHub", profile.GitHubHandle);
        }
    }
}

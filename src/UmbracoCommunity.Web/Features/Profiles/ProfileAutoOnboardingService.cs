using Microsoft.Extensions.Logging;
using UmbracoCommunity.Web.Features.Profiles.Data;
using UmbracoCommunity.Web.Features.Profiles.Data.Entities;

namespace UmbracoCommunity.Web.Features.Profiles;

/// <summary>
/// Sets up a signed-in member's profile without asking them anything: creates the profile row,
/// copies their GitHub name, company and bio into their own display name, headline and bio, and
/// marks onboarding completed. Used by <see cref="Middleware.OnboardingRedirectMiddleware"/> the
/// first time a member without a completed profile is seen.
/// </summary>
/// <remarks>
/// Only fills a field the member hasn't written themselves (it is still null), so a member who
/// was part way through the old onboarding wizard keeps their own text. The GitHub values become
/// the member's actual profile text, not fallbacks. Visibility is not touched: profiles stay hidden
/// until the member chooses to show them. A failed GitHub lookup still completes onboarding with
/// the name (and leaves the cached GitHub defaults unfetched, so they are tried again later).
/// Never throws, except when cancelled.
/// </remarks>
public class ProfileAutoOnboardingService
{
    private readonly MemberProfileStore _store;
    private readonly GitHubProfileClient _gitHubClient;
    private readonly ILogger<ProfileAutoOnboardingService> _logger;

    public ProfileAutoOnboardingService(MemberProfileStore store, GitHubProfileClient gitHubClient, ILogger<ProfileAutoOnboardingService> logger)
    {
        _store = store;
        _gitHubClient = gitHubClient;
        _logger = logger;
    }

    /// <summary>
    /// Completes the member's onboarding when it isn't completed yet. Returns true when it was set
    /// up by this call; false when it was already completed, there is no handle, or setting it up failed.
    /// </summary>
    /// <param name="memberKey">The member's <c>Key</c>.</param>
    /// <param name="gitHubHandle">The member's GitHub handle (the member's <c>UserName</c>).</param>
    /// <param name="gitHubName">The member's GitHub name (the member's <c>Name</c>); the handle is used when it is empty.</param>
    public async Task<bool> EnsureOnboardedAsync(Guid memberKey, string? gitHubHandle, string? gitHubName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(gitHubHandle))
        {
            return false;
        }

        try
        {
            var existing = await _store.GetByMemberKeyAsync(memberKey, cancellationToken);
            if (existing?.OnboardingStatus == OnboardingStatus.Completed)
            {
                return false;
            }

            var name = ProfileText.Normalize(gitHubName) ?? gitHubHandle;
            var profile = await _store.GetOrStartOnboardingAsync(memberKey, gitHubHandle, name, cancellationToken);

            string? headline = profile.GitHubCompany;
            string? bio = profile.GitHubBio;

            if (!profile.GitHubFetchedUtc.HasValue)
            {
                var result = await _gitHubClient.GetProfileAsync(gitHubHandle, cancellationToken);
                if (result.Succeeded)
                {
                    await _store.SetGitHubDefaultsAsync(memberKey, result.Bio, result.Company, DateTime.UtcNow, cancellationToken);
                    headline = result.Company;
                    bio = result.Bio;
                }
                else
                {
                    _logger.LogWarning("Couldn't read the GitHub profile of {Handle}; onboarding them with their name only", gitHubHandle);
                }
            }

            await _store.FillMissingProfileTextAsync(
                memberKey,
                ProfileText.NormalizeAndTruncate(name, ProfileText.MaxDisplayNameLength),
                ProfileText.NormalizeAndTruncate(headline, ProfileText.MaxHeadlineLength),
                ProfileText.NormalizeAndTruncate(bio, ProfileText.MaxBioLength),
                cancellationToken);

            await _store.CompleteOnboardingAsync(memberKey, cancellationToken);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Couldn't set up the profile of {Handle}", gitHubHandle);
            return false;
        }
    }
}

using UmbracoCommunity.Web.Features.OpenBadges.Infrastructure;
using UmbracoCommunity.Web.Features.Profiles.Models;

namespace UmbracoCommunity.Web.Features.Profiles;

/// <summary>
/// The member's own profile header, for the account page's preview of it: the same identity their public profile
/// shows (name, headline, bio, photo, the links they haven't hidden and the badges they chose to show, from
/// <see cref="IProfileDataProvider"/>), with
/// their MVP years by the same rule as the profile's MVP pill (<see cref="ProfileMvp.Resolve"/>, through
/// <see cref="IMvpBadgeEligibility"/>). A hidden profile is included, as only its owner asks for it.
/// </summary>
public sealed class AccountProfileIdentityService
{
    private readonly IProfileDataProvider _profileDataProvider;
    private readonly IMvpBadgeEligibility _mvpEligibility;

    public AccountProfileIdentityService(IProfileDataProvider profileDataProvider, IMvpBadgeEligibility mvpEligibility)
    {
        _profileDataProvider = profileDataProvider;
        _mvpEligibility = mvpEligibility;
    }

    /// <summary>The identity the public profile of <paramref name="handle"/> shows, or null when they have no profile yet.</summary>
    public async Task<ProfileIdentity?> GetAsync(string handle, CancellationToken cancellationToken = default) =>
        (await GetHeaderAsync(handle, cancellationToken))?.Identity;

    /// <summary>
    /// The identity and the badges the public profile hero of <paramref name="handle"/> shows, or null when they have no
    /// profile yet.
    /// </summary>
    public async Task<AccountProfileHeader?> GetHeaderAsync(string handle, CancellationToken cancellationToken = default)
    {
        var profile = await _profileDataProvider.GetProfileAsync(handle, includeHidden: true, cancellationToken);
        if (profile == null)
        {
            return null;
        }

        var mvp = await _mvpEligibility.GetStatusAsync(handle, cancellationToken);
        var identity = mvp.Years.Count == 0
            ? profile.Identity
            : profile.Identity with { IsMvp = true, MvpYears = mvp.Years, IsCurrentMvp = mvp.IsCurrent };
        return new AccountProfileHeader(identity, profile.Badges);
    }
}

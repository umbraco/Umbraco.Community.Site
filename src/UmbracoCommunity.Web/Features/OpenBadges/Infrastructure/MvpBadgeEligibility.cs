using UmbracoCommunity.Web.Features.Mvp.Infrastructure;
using UmbracoCommunity.Web.Features.OpenBadges.Models;
using UmbracoCommunity.Web.Features.Profiles;

namespace UmbracoCommunity.Web.Features.OpenBadges.Infrastructure;

/// <summary>
/// <see cref="IMvpBadgeEligibility"/> using <see cref="ProfileMvp.Resolve"/>: the MVP years the external content
/// platform reports for the handle (cached by <see cref="ProfileActivityService"/>), otherwise the MVP list in the repo.
/// So a member gets a badge exactly when their profile shows them as an MVP.
/// </summary>
internal sealed class MvpBadgeEligibility(ProfileActivityService profileActivityService, IMvpDataService mvpDataService)
    : IMvpBadgeEligibility
{
    public async Task<MvpStatus> GetStatusAsync(string handle, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(handle))
        {
            return MvpStatus.None;
        }

        var platformYears = await profileActivityService.GetPlatformMvpYearsAsync(handle, cancellationToken);
        var (years, isCurrent) = ProfileMvp.Resolve(handle, platformYears, mvpDataService.GetAll());
        return years.Count == 0 ? MvpStatus.None : new MvpStatus(years, isCurrent);
    }
}

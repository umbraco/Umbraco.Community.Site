using UmbracoCommunity.Web.Features.OpenBadges.Models;

namespace UmbracoCommunity.Web.Features.OpenBadges.Infrastructure;

/// <summary>Which years a GitHub handle was an Umbraco MVP, by the same rule the public profile uses.</summary>
public interface IMvpBadgeEligibility
{
    Task<MvpStatus> GetStatusAsync(string handle, CancellationToken cancellationToken = default);
}

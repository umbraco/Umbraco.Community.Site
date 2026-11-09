using UmbracoCommunity.Web.Features.Mvp.Models;
using UmbracoCommunity.Web.Features.Profiles.Models;

namespace UmbracoCommunity.Web.Features.Profiles;

/// <summary>
/// Works out which MVP years a profile shows. The years come from the external content platform when it
/// reports any, otherwise from the MVP list kept in the repo (matched on GitHub handle).
/// </summary>
internal static class ProfileMvp
{
    /// <summary>
    /// Returns the identity with <see cref="ProfileIdentity.IsMvp"/>, <see cref="ProfileIdentity.MvpYears"/> (newest
    /// first) and <see cref="ProfileIdentity.IsCurrentMvp"/> filled in. A member is a current MVP when their newest year
    /// is the newest year known anywhere, platform or file.
    /// </summary>
    public static ProfileIdentity Apply(
        ProfileIdentity identity,
        IReadOnlyList<int>? platformYears,
        IReadOnlyList<MvpYear> listedYears)
    {
        var years = platformYears is { Count: > 0 }
            ? platformYears
            : YearsFromList(identity.GitHubHandle, listedYears);

        var ordered = years.Distinct().OrderByDescending(y => y).ToList();
        if (ordered.Count == 0)
        {
            return identity;
        }

        var newestKnown = Math.Max(ordered[0], listedYears.Count > 0 ? listedYears.Max(y => y.Year) : 0);
        return identity with
        {
            IsMvp = true,
            MvpYears = ordered,
            IsCurrentMvp = ordered[0] == newestKnown,
        };
    }

    private static IReadOnlyList<int> YearsFromList(string? handle, IReadOnlyList<MvpYear> listedYears) =>
        string.IsNullOrWhiteSpace(handle)
            ? []
            : listedYears
                .Where(y => y.Members.Any(m => string.Equals(m.GitHubHandle, handle, StringComparison.OrdinalIgnoreCase)))
                .Select(y => y.Year)
                .ToList();
}

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
        var (years, isCurrent) = Resolve(identity.GitHubHandle, platformYears, listedYears);
        if (years.Count == 0)
        {
            return identity;
        }

        return identity with
        {
            IsMvp = true,
            MvpYears = years,
            IsCurrentMvp = isCurrent,
        };
    }

    /// <summary>
    /// The MVP years of <paramref name="handle"/> (newest first, empty when never an MVP) and whether the newest of them
    /// is the newest year known anywhere. The same rule <see cref="Apply"/> uses, for callers without a profile.
    /// </summary>
    public static (IReadOnlyList<int> Years, bool IsCurrent) Resolve(
        string? handle,
        IReadOnlyList<int>? platformYears,
        IReadOnlyList<MvpYear> listedYears)
    {
        var years = platformYears is { Count: > 0 }
            ? platformYears
            : YearsFromList(handle, listedYears);

        var ordered = years.Distinct().OrderByDescending(y => y).ToList();
        if (ordered.Count == 0)
        {
            return ([], false);
        }

        var newestKnown = Math.Max(ordered[0], listedYears.Count > 0 ? listedYears.Max(y => y.Year) : 0);
        return (ordered, ordered[0] == newestKnown);
    }

    private static IReadOnlyList<int> YearsFromList(string? handle, IReadOnlyList<MvpYear> listedYears) =>
        string.IsNullOrWhiteSpace(handle)
            ? []
            : listedYears
                .Where(y => y.Members.Any(m => string.Equals(m.GitHubHandle, handle, StringComparison.OrdinalIgnoreCase)))
                .Select(y => y.Year)
                .ToList();
}

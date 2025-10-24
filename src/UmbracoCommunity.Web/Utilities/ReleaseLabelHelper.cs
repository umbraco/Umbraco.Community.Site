using UmbracoCommunity.Web.Features.GitHubSync.Infrastructure;

namespace UmbracoCommunity.Web.Utilities;

public static class ReleaseLabelHelper
{
    /// <summary>
    /// Checks if a label is a release label (e.g., "release/1.0.0" or "cms/release/1.0.0").
    /// </summary>
    public static bool IsReleaseLabel(string label)
    {
        if (string.IsNullOrWhiteSpace(label))
            return false;

        return label.StartsWith("release/", StringComparison.OrdinalIgnoreCase) ||
               label.Contains("/release/", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if a release label is valid for the given repository.
    /// Valid labels are: "release/X.Y.Z" or "{prefix}/release/X.Y.Z" where prefix matches the repo's AnnouncementsPrefix.
    /// </summary>
    public static bool IsValidReleaseLabelForRepository(string label, RepositoryConfig? repoConfig)
    {
        if (!IsReleaseLabel(label))
            return false;

        // Always allow unprefixed "release/" labels
        if (label.StartsWith("release/", StringComparison.OrdinalIgnoreCase))
            return true;

        // If the repo has an AnnouncementsPrefix, allow labels with that prefix
        if (repoConfig?.HasAnnouncementsPrefix == true &&
            !string.IsNullOrEmpty(repoConfig.AnnouncementsPrefix))
        {
            var prefixPattern = $"{repoConfig.AnnouncementsPrefix}/release/";
            if (label.StartsWith(prefixPattern, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        // Reject all other prefixed labels (e.g., "forms/release/", "commerce/release/", etc.)
        return false;
    }

    /// <summary>
    /// Checks if a release label has a valid SemVer version.
    /// </summary>
    public static bool HasValidSemVer(string label)
    {
        if (!IsReleaseLabel(label))
            return false;

        var version = ExtractVersion(label);
        return SemVerHelper.IsValidSemVer(version);
    }

    /// <summary>
    /// Extracts version string from label. Handles both "release/X.Y.Z" and "{prefix}/release/X.Y.Z" patterns.
    /// </summary>
    public static string ExtractVersion(string label)
    {
        // Handle both "release/X.Y.Z" and "prefix/release/X.Y.Z" patterns
        var releasePart = "/release/";
        var releaseIndex = label.IndexOf(releasePart, StringComparison.OrdinalIgnoreCase);

        if (releaseIndex >= 0)
        {
            // Extract everything after "/release/"
            return label.Substring(releaseIndex + releasePart.Length);
        }

        // Fallback: assume it starts with "release/"
        return label.Replace("release/", "", StringComparison.OrdinalIgnoreCase).Trim();
    }

    /// <summary>
    /// Normalizes a release label by removing any prefix.
    /// E.g., "cms/release/17.0.0" -> "release/17.0.0", "release/17.0.0" -> "release/17.0.0"
    /// </summary>
    public static string Normalize(string label)
    {
        var releasePart = "/release/";
        var releaseIndex = label.IndexOf(releasePart, StringComparison.OrdinalIgnoreCase);

        if (releaseIndex >= 0)
        {
            // Found a prefix before "/release/", extract "release/X.Y.Z" part
            return "release" + label.Substring(releaseIndex + releasePart.Length - 1);
        }

        // No prefix, return as-is
        return label;
    }

    /// <summary>
    /// Checks if a label is a valid release label for the repository with a valid SemVer version.
    /// Combines IsValidReleaseLabelForRepository and HasValidSemVer checks.
    /// </summary>
    public static bool IsValidReleaseLabelWithSemVer(string label, RepositoryConfig? repoConfig)
    {
        return IsValidReleaseLabelForRepository(label, repoConfig) && HasValidSemVer(label);
    }
}

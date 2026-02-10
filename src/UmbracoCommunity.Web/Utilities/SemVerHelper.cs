namespace UmbracoCommunity.Web.Utilities;

public static class SemVerHelper
{
    private static readonly Version DefaultVersion = new(0, 0, 0);

    /// <summary>
    /// Checks if a version string is a valid SemVer version.
    /// Valid versions must have at least a major version number (e.g., "1.0", "1.0.0", "1.0.0-rc1").
    /// </summary>
    public static bool IsValidSemVer(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
            return false;

        // Remove build metadata (after +)
        var versionWithoutBuild = version.Split('+')[0];

        // Remove pre-release identifier (after -)
        var stableVersion = versionWithoutBuild.Split('-')[0];

        // Try to parse as a valid Version (must have at least major.minor or major.minor.patch)
        return Version.TryParse(stableVersion, out _);
    }

    /// <summary>
    /// Checks if a version string is a pre-release according to SemVer 2.0 specification.
    /// A pre-release version is identified by a hyphen followed by dot-separated identifiers.
    /// </summary>
    public static bool IsPreRelease(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
            return false;

        // Remove any build metadata (after +)
        var versionWithoutBuild = version.Split('+')[0];

        // Check for pre-release identifier (after -)
        var parts = versionWithoutBuild.Split('-');
        return parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]);
    }

    /// <summary>
    /// Extracts the stable version (major.minor.patch) from a SemVer string.
    /// Removes pre-release and build metadata.
    /// </summary>
    public static string GetStableVersion(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
            return string.Empty;

        // Remove build metadata (after +)
        var versionWithoutBuild = version.Split('+')[0];

        // Remove pre-release identifier (after -)
        var stableVersion = versionWithoutBuild.Split('-')[0];

        return stableVersion;
    }

    /// <summary>
    /// Parses a version string into its components.
    /// </summary>
    public static (string StableVersion, string? PreRelease, string? BuildMetadata) Parse(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
            return (string.Empty, null, null);

        string? buildMetadata = null;
        var versionWithoutBuild = version;

        // Extract build metadata (after +)
        var plusIndex = version.IndexOf('+');
        if (plusIndex >= 0)
        {
            buildMetadata = version.Substring(plusIndex + 1);
            versionWithoutBuild = version.Substring(0, plusIndex);
        }

        string? preRelease = null;
        var stableVersion = versionWithoutBuild;

        // Extract pre-release (after -)
        var dashIndex = versionWithoutBuild.IndexOf('-');
        if (dashIndex >= 0)
        {
            preRelease = versionWithoutBuild.Substring(dashIndex + 1);
            stableVersion = versionWithoutBuild.Substring(0, dashIndex);
        }

        return (stableVersion, preRelease, buildMetadata);
    }

    /// <summary>
    /// Parses a version string into a Version object, stripping any pre-release or build metadata.
    /// Returns Version(0,0,0) if parsing fails.
    /// </summary>
    public static Version ParseToVersion(string versionString)
    {
        if (string.IsNullOrWhiteSpace(versionString))
            return DefaultVersion;

        var stableVersion = GetStableVersion(versionString.Trim());
        return Version.TryParse(stableVersion, out var version) ? version : DefaultVersion;
    }

    /// <summary>
    /// Parses a version string into a Version object and pre-release identifier.
    /// Returns (Version(0,0,0), "") if parsing fails.
    /// </summary>
    public static (Version Version, string PreRelease) ParseToVersionWithPreRelease(string versionString)
    {
        if (string.IsNullOrWhiteSpace(versionString))
            return (DefaultVersion, string.Empty);

        versionString = versionString.Trim();
        var (stableVersion, preRelease, _) = Parse(versionString);

        var version = Version.TryParse(stableVersion, out var v) ? v : DefaultVersion;
        return (version, preRelease ?? string.Empty);
    }

    /// <summary>
    /// Parses a release label (e.g., "release/14.0.0" or "release/14.0.0-rc1") into a Version object.
    /// Strips the "release/" prefix and any pre-release suffix.
    /// Returns Version(0,0,0) if parsing fails.
    /// </summary>
    public static Version ParseReleaseLabel(string releaseLabel)
    {
        if (string.IsNullOrWhiteSpace(releaseLabel))
            return DefaultVersion;

        var versionString = releaseLabel.Replace("release/", "").Trim();
        return ParseToVersion(versionString);
    }
}

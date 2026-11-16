namespace UmbracoCommunity.Web.Features.Profiles;

public sealed class ProfileActivityOptions
{
    public const string SectionName = "ProfileActivity";

    /// <summary>How long a member's activity is kept before it is fetched again.</summary>
    public int CacheMinutes { get; set; } = 30;

    /// <summary>How long an empty answer is kept after the platform could not be reached or errored.</summary>
    public int FailureCacheMinutes { get; set; } = 2;

    /// <summary>
    /// The newest items kept per section, however old. Everything from the last 12 months is kept as well (up to 500 per
    /// section), so this mostly matters for a quiet section.
    /// </summary>
    public int ItemsPerSection { get; set; } = 10;
}

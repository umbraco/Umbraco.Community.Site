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

    /// <summary>
    /// Ask the default Decision profile (Umbraco.AI, experimental) which social posts are the same post shared on two
    /// platforms (on any platform, at most six hours apart). With the check off, or without a model, posts are never merged. Needs <c>Umbraco:AI:Experimental:Decision</c> and a
    /// default Decision profile; without them it does nothing.
    /// </summary>
    public bool AiCrossPostCheck { get; set; } = true;

    /// <summary>The probability (0 to 1) at or above which the model's answer counts as "the same post".</summary>
    public double AiCrossPostThreshold { get; set; } = 0.5;

    /// <summary>The most new questions asked per profile load, closest in time first. Every answer is kept in the database, so a pair is asked once, ever; pairs over the limit wait for the next load (the profile is then cached only for a minute).</summary>
    public int AiCrossPostMaxPairs { get; set; } = 25;
}

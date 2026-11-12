namespace UmbracoCommunity.Web.Features.Profiles.Matching;

/// <summary>The confidence recorded with each kind of automatic match decision (0 to 1), see <see cref="ProfileLinkMatchingService"/>.</summary>
public sealed class ProfileMatchingOptions
{
    public const string SectionName = "ProfileMatching";

    /// <summary>For a link that is confirmed: the member's own GitHub profile, or one the platform already ties to them.</summary>
    public double ConfirmedConfidence { get; set; } = 1.0;

    /// <summary>For a link that is held for review because the platform knows nothing about it.</summary>
    public double HeldUnknownConfidence { get; set; } = 0.3;

    /// <summary>For a link that is held for review because the platform ties it to a different profile.</summary>
    public double HeldConflictConfidence { get; set; } = 0.1;
}

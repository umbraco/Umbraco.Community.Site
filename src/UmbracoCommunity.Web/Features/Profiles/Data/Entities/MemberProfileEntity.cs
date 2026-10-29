namespace UmbracoCommunity.Web.Features.Profiles.Data.Entities;

public enum OnboardingStatus
{
    NotStarted = 0,
    InProgress = 1,
    Completed = 2
}

/// <summary>
/// One row per community member who has started or completed onboarding. Keyed on the
/// stable Umbraco member <see cref="MemberKey"/> rather than the GitHub handle, since the
/// handle is only a denormalized cache synced from GitHub claims (see <see cref="GitHubHandle"/>).
/// </summary>
public class MemberProfileEntity
{
    public int Id { get; set; }

    /// <summary>The Umbraco member's stable <c>IMember.Key</c> — the durable identity.</summary>
    public Guid MemberKey { get; set; }

    /// <summary>
    /// Denormalized cache of the member's GitHub handle, synced when onboarding starts.
    /// Lets the profile content finder/provider resolve a profile by URL slug without a
    /// member-manager round trip. Not re-synced on every login.
    /// </summary>
    public string GitHubHandle { get; set; } = string.Empty;

    /// <summary>
    /// Denormalized cache of the member's display name from GitHub, synced when onboarding starts.
    /// The default when the member hasn't chosen their own (see <see cref="DisplayNameOverride"/>).
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>The member's own display name. Null = use <see cref="DisplayName"/>.</summary>
    public string? DisplayNameOverride { get; set; }

    /// <summary>The member's own headline. Null = use <see cref="GitHubCompany"/>.</summary>
    public string? Headline { get; set; }

    /// <summary>The member's own bio. Null = use <see cref="GitHubBio"/>.</summary>
    public string? Bio { get; set; }

    /// <summary>The bio from the member's public GitHub profile, cached so it is fetched once rather than per page view.</summary>
    public string? GitHubBio { get; set; }

    /// <summary>The company from the member's public GitHub profile, cached alongside <see cref="GitHubBio"/>.</summary>
    public string? GitHubCompany { get; set; }

    /// <summary>When <see cref="GitHubBio"/> and <see cref="GitHubCompany"/> were fetched. Null = not fetched yet.</summary>
    public DateTime? GitHubFetchedUtc { get; set; }

    /// <summary>The display name to show: the member's own, otherwise the GitHub one.</summary>
    public string EffectiveDisplayName => DisplayNameOverride ?? DisplayName;

    /// <summary>The headline to show: the member's own, otherwise their GitHub company.</summary>
    public string? EffectiveHeadline => Headline ?? GitHubCompany;

    /// <summary>The bio to show: the member's own, otherwise their GitHub bio.</summary>
    public string? EffectiveBio => Bio ?? GitHubBio;

    /// <summary>Umbraco Media item key for an uploaded custom avatar. Null = use the GitHub avatar default.</summary>
    public Guid? AvatarMediaKey { get; set; }

    public OnboardingStatus OnboardingStatus { get; set; } = OnboardingStatus.NotStarted;

    public DateTime? OnboardingStartedUtc { get; set; }

    public DateTime? OnboardingCompletedUtc { get; set; }

    /// <summary>
    /// True once the member has chosen to show their profile. New profiles start hidden, so
    /// the public profile page renders for other visitors exactly as it does for a handle that
    /// has no profile, while the owner can still see and edit it.
    /// </summary>
    public bool IsProfileVisible { get; set; }

    /// <summary>The external platform's internal profile id, once it has a real claim endpoint. Null today.</summary>
    public string? PlatformProfileId { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime UpdatedUtc { get; set; }

    public List<MemberFeedEntity> Feeds { get; set; } = [];
}

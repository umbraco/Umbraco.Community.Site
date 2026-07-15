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

    /// <summary>Denormalized cache of the member's display name, synced when onboarding starts.</summary>
    public string DisplayName { get; set; } = string.Empty;

    public string? Bio { get; set; }

    /// <summary>Umbraco Media item key for an uploaded custom avatar. Null = use the GitHub avatar default.</summary>
    public Guid? AvatarMediaKey { get; set; }

    public OnboardingStatus OnboardingStatus { get; set; } = OnboardingStatus.NotStarted;

    public DateTime? OnboardingStartedUtc { get; set; }

    public DateTime? OnboardingCompletedUtc { get; set; }

    /// <summary>Sphere's internal profile id, once Sphere has a real claim endpoint. Null today.</summary>
    public string? SphereProfileId { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime UpdatedUtc { get; set; }

    public List<MemberFeedEntity> Feeds { get; set; } = [];
}

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
    /// Only read when <see cref="DisplayNameOverride"/> is somehow empty, so the profile always has a name.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// The member's own display name: copied from GitHub at onboarding, then theirs to change. Never
    /// emptied by the member (the API refuses an empty name).
    /// </summary>
    public string? DisplayNameOverride { get; set; }

    /// <summary>
    /// The member's own headline: copied from their GitHub company at onboarding, then theirs to change.
    /// Null = no headline is shown. There is no fallback to <see cref="GitHubCompany"/>.
    /// </summary>
    public string? Headline { get; set; }

    /// <summary>
    /// The member's own bio: copied from their GitHub bio at onboarding, then theirs to change.
    /// Null = no bio is shown. There is no fallback to <see cref="GitHubBio"/>.
    /// </summary>
    public string? Bio { get; set; }

    /// <summary>
    /// The bio from the member's public GitHub profile, fetched once so onboarding can copy it into <see cref="Bio"/>.
    /// Never shown itself.
    /// </summary>
    public string? GitHubBio { get; set; }

    /// <summary>
    /// The company from the member's public GitHub profile, fetched alongside <see cref="GitHubBio"/> so onboarding can
    /// copy it into <see cref="Headline"/>. Never shown itself.
    /// </summary>
    public string? GitHubCompany { get; set; }

    /// <summary>When <see cref="GitHubBio"/> and <see cref="GitHubCompany"/> were fetched. Null = not fetched yet.</summary>
    public DateTime? GitHubFetchedUtc { get; set; }

    /// <summary>The display name to show: the member's own (always set after onboarding), else the GitHub name cached at sign-up.</summary>
    public string EffectiveDisplayName => ProfileText.Normalize(DisplayNameOverride) ?? DisplayName;

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

    /// <summary>
    /// The activity sections (see <see cref="Models.PlatformActivityGroups"/>) the member has switched
    /// off. Empty = every section is shown.
    /// </summary>
    public List<string> HiddenSections { get; set; } = [];

    /// <summary>The ids of single activities from the external content platform the member has hidden.</summary>
    public List<string> HiddenActivityIds { get; set; } = [];

    /// <summary>
    /// The ids of the Umbraco forum badges the member chose to show on their profile (see
    /// <see cref="ForumBadges.ForumBadgeService"/>). Empty = none. A badge the member no longer holds is skipped when shown.
    /// </summary>
    public List<int> ShownForumBadgeIds { get; set; } = [];

    // Unused since the activity graph switch was removed; drop with the next migration.
    public bool HideActivityGraph { get; set; }

    // Unused since the preview cards switch was removed; drop with the next migration.
    public bool HideTimelineCards { get; set; }

    // Unused since the per-item "plain line" choice was removed; drop with the next migration.
    public List<string> NoCardActivityIds { get; set; } = [];

    public DateTime CreatedUtc { get; set; }

    public DateTime UpdatedUtc { get; set; }

    public List<MemberFeedEntity> Feeds { get; set; } = [];

    public List<MemberPinnedItemEntity> PinnedItems { get; set; } = [];
}

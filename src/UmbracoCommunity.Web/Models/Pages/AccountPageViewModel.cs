using Umbraco.Cms.Core.Models.PublishedContent;
using UmbracoCommunity.Web.Features.Profiles.Models;

namespace UmbracoCommunity.Web.Models.Pages;

public class AccountPageViewModel(IPublishedContent currentPage) : PageViewModelBase(currentPage)
{
    public string? DisplayName { get; set; }
    public string? GitHubHandle { get; set; }
    public string? Email { get; set; }
    public string? AvatarUrl { get; set; }

    /// <summary>True when the member has uploaded their own photo instead of using their GitHub avatar.</summary>
    public bool HasCustomAvatar { get; set; }

    /// <summary>The member's own display name. Null when their GitHub name is used instead.</summary>
    public string? CustomDisplayName { get; set; }

    /// <summary>The member's GitHub name, used when they haven't chosen their own.</summary>
    public string? DefaultDisplayName { get; set; }

    /// <summary>The member's own headline. Null when their GitHub company is used instead.</summary>
    public string? CustomHeadline { get; set; }

    /// <summary>The company from the member's GitHub profile, used as the headline when they haven't written their own.</summary>
    public string? GitHubHeadline { get; set; }

    /// <summary>The member's own bio. Null when their GitHub bio is used instead.</summary>
    public string? CustomBio { get; set; }

    /// <summary>The bio from the member's GitHub profile, used when they haven't written their own.</summary>
    public string? GitHubBio { get; set; }

    /// <summary>
    /// True once the member's profile has been set up (automatically, on sign-in) — gates the profile editors and the
    /// public profile link. False only when that setup failed; it is retried on the next page load.
    /// </summary>
    public bool OnboardingCompleted { get; set; }

    /// <summary>True when the member has chosen to show their profile to everyone. New profiles start hidden.</summary>
    public bool IsProfileVisible { get; set; }

    /// <summary>
    /// True when the member may choose what the activity sections of their profile show (see
    /// <c>ProfileAccessService.CanChooseProfileDisplayAsync</c>).
    /// </summary>
    public bool CanChooseProfileDisplay { get; set; }

    /// <summary>True when the member may add activity to their profile by hand (see <c>ProfileAccessService.CanAddActivityAsync</c>).</summary>
    public bool CanAddActivity { get; set; }

    /// <summary>
    /// True when the member is offered links found for them and can say a source is not theirs (see
    /// <c>ProfileAccessService.CanMatchLinksAsync</c>).
    /// </summary>
    public bool CanMatchLinks { get; set; }

    /// <summary>The member's public profile URL, once claimed.</summary>
    public string? ProfileUrl { get; set; }

    /// <summary>
    /// The year of the Umbraco MVP badge the member can download, when they are a current MVP and that year's badge
    /// can be issued. Null hides the badge section.
    /// </summary>
    public int? MvpBadgeYear { get; set; }

    /// <summary>Where the member downloads their badge with the Open Badges data baked in.</summary>
    public string? MvpBadgeDownloadUrl { get; set; }

    /// <summary>The plain badge image, shown as a preview next to the download link.</summary>
    public string? MvpBadgeImageUrl { get; set; }

    /// <summary>
    /// What the member's public profile shows in its header (name, headline, bio, photo, links, MVP), for the account
    /// page's preview of it. Set for every signed-in member; before onboarding only the name and photo are known.
    /// </summary>
    public ProfileIdentity ProfileIdentity { get; set; } = null!;

    /// <summary>The forum badges the member chose to show, as the header's badge row shows them. Empty for none.</summary>
    public IReadOnlyList<ProfileBadge> ProfileBadges { get; set; } = [];

    /// <summary>When the external platform last synced this member's profile data, if known.</summary>
    public DateTimeOffset? LastSyncedAt { get; set; }
}

using Umbraco.Cms.Core.Models.PublishedContent;

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

    /// <summary>True once the member has completed onboarding — gates showing the public profile link.</summary>
    public bool OnboardingCompleted { get; set; }

    /// <summary>True when the member has chosen to show their profile to everyone. New profiles start hidden.</summary>
    public bool IsProfileVisible { get; set; }

    /// <summary>The member's public profile URL, once claimed.</summary>
    public string? ProfileUrl { get; set; }

    /// <summary>The onboarding page URL, shown as a nudge when onboarding isn't complete yet.</summary>
    public string? OnboardingUrl { get; set; }

    /// <summary>When the external platform last synced this member's profile data, if known.</summary>
    public DateTimeOffset? LastSyncedAt { get; set; }
}

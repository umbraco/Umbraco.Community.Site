using Umbraco.Cms.Core.Models.PublishedContent;

namespace UmbracoCommunity.Web.Models.Pages;

public class AccountPageViewModel(IPublishedContent currentPage) : PageViewModelBase(currentPage)
{
    public string? DisplayName { get; set; }
    public string? GitHubHandle { get; set; }
    public string? Email { get; set; }
    public string? AvatarUrl { get; set; }

    /// <summary>True once the member has completed onboarding — gates showing the public profile link.</summary>
    public bool OnboardingCompleted { get; set; }

    /// <summary>The member's public profile URL, once claimed.</summary>
    public string? ProfileUrl { get; set; }

    /// <summary>The onboarding page URL, shown as a nudge when onboarding isn't complete yet.</summary>
    public string? OnboardingUrl { get; set; }

    /// <summary>When the external platform last synced this member's profile data, if known.</summary>
    public DateTimeOffset? LastSyncedAt { get; set; }
}

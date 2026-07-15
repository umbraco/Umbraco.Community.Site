using Umbraco.Cms.Core.Models.PublishedContent;

namespace UmbracoCommunity.Web.Models.Pages;

public class OnboardingPageViewModel(IPublishedContent currentPage) : PageViewModelBase(currentPage)
{
    /// <summary>
    /// The tenant's Community Profile page URL (without a handle segment), used by the
    /// wizard to build the final redirect once onboarding completes. Null if the tenant
    /// hasn't set one up yet.
    /// </summary>
    public string? ProfileBaseUrl { get; init; }

    /// <summary>
    /// False when an anonymous visitor reaches this page directly (e.g. a shared "get your
    /// profile" link) — the view shows a GitHub sign-in prompt instead of the wizard in
    /// that case, rather than bouncing them away.
    /// </summary>
    public bool IsSignedIn { get; init; }

    /// <summary>Editor-configured link (OnboardingPage.DevRelContactForm) for "want to change your handle?".</summary>
    public string? DevRelContactUrl { get; init; }

    public string? DevRelContactTarget { get; init; }
}

using Umbraco.Cms.Core.Models.PublishedContent;

namespace UmbracoCommunity.Web.Models.Pages;

/// <summary>
/// The onboarding page as anonymous visitors see it: a GitHub sign-in prompt. Signed-in members
/// are onboarded automatically and redirected to their account page, so they never get here.
/// </summary>
public class OnboardingPageViewModel(IPublishedContent currentPage) : PageViewModelBase(currentPage);

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Web.Common.Controllers;
using Umbraco.Extensions;
using UmbracoCommunity.Web.Extensions;
using UmbracoCommunity.Web.Models.Pages;
using UmbracoCommunity.Web.Models.PublishedModels;

namespace UmbracoCommunity.Web.Controllers.Render;

public class OnboardingPageController : RenderController
{
    private readonly IMemberManager _memberManager;
    private readonly IPublishedUrlProvider _publishedUrlProvider;

    public OnboardingPageController(
        ILogger<OnboardingPageController> logger,
        ICompositeViewEngine compositeViewEngine,
        IUmbracoContextAccessor umbracoContextAccessor,
        IMemberManager memberManager,
        IPublishedUrlProvider publishedUrlProvider)
        : base(logger, compositeViewEngine, umbracoContextAccessor)
    {
        _memberManager = memberManager;
        _publishedUrlProvider = publishedUrlProvider;
    }

    [NonAction]
    public sealed override IActionResult Index() => throw new NotImplementedException();

    public IActionResult Index(CancellationToken cancellationToken)
    {
        var currentPage = CurrentPage ?? throw new InvalidOperationException($"Cannot build view model as {nameof(CurrentPage)} is null.");

        // Anonymous visitors can land here directly (e.g. a devrel-shared "get your
        // profile" link) — show a sign-in prompt in the view rather than bouncing them
        // away, so a direct link to onboarding actually works without being signed in first.
        var profilePage = currentPage.GetSingletonPage<CommunityProfilePage>();
        var devRelContactLink = currentPage.As<OnboardingPage>().DevRelContactForm;
        var viewModel = new OnboardingPageViewModel(currentPage)
        {
            // Absolute so the wizard can show the member the actual link their profile will
            // live at (scheme + host included) — this varies per environment (localhost in
            // dev, the real domain elsewhere), which a relative path wouldn't communicate.
            ProfileBaseUrl = profilePage?.Url(_publishedUrlProvider, mode: UrlMode.Absolute),
            IsSignedIn = _memberManager.IsLoggedIn(),
            DevRelContactUrl = devRelContactLink?.Url,
            DevRelContactTarget = devRelContactLink?.Target,
            DevRelContactTitle = devRelContactLink?.Name,
        };

        return CurrentTemplate(viewModel);
    }
}

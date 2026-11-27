using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.Extensions.Logging;
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

        // Members are onboarded automatically (see OnboardingRedirectMiddleware), so a signed-in
        // member has nothing to do here: send them to their account page instead.
        if (_memberManager.IsLoggedIn()
            && currentPage.GetSingletonPage<AccountPage>()?.Url(_publishedUrlProvider) is { Length: > 0 } accountUrl)
        {
            return Redirect(accountUrl);
        }

        // Anonymous visitors can land here directly (e.g. a devrel-shared "get your profile" link):
        // the view shows them a GitHub sign-in prompt rather than bouncing them away.
        var viewModel = new OnboardingPageViewModel(currentPage);

        return CurrentTemplate(viewModel);
    }
}

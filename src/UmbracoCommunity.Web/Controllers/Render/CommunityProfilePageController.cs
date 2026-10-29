using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Web.Common.Controllers;
using Umbraco.Extensions;
using UmbracoCommunity.Web.Extensions;
using UmbracoCommunity.Web.Features.Profiles;
using UmbracoCommunity.Web.Features.Profiles.Models;
using UmbracoCommunity.Web.Models.Pages;
using UmbracoCommunity.Web.Models.PublishedModels;
using UmbracoCommunity.Web.Routing;

namespace UmbracoCommunity.Web.Controllers.Render;

public class CommunityProfilePageController : RenderController
{
    private readonly ProfileAccessService _profileAccessService;
    private readonly IPublishedUrlProvider _publishedUrlProvider;

    public CommunityProfilePageController(
        ILogger<CommunityProfilePageController> logger,
        ICompositeViewEngine compositeViewEngine,
        IUmbracoContextAccessor umbracoContextAccessor,
        ProfileAccessService profileAccessService,
        IPublishedUrlProvider publishedUrlProvider)
        : base(logger, compositeViewEngine, umbracoContextAccessor)
    {
        _profileAccessService = profileAccessService;
        _publishedUrlProvider = publishedUrlProvider;
    }

    [NonAction]
    public sealed override IActionResult Index() => throw new NotImplementedException();

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var currentPage = CurrentPage ?? throw new InvalidOperationException($"Cannot build view model as {nameof(CurrentPage)} is null.");

        // The bare page URL (no handle segment) is only ever reached via Umbraco's built-in
        // content finder, not CommunityProfileContentFinder (which requires a trailing
        // segment) — there's nothing to render at that URL, so 404 rather than throw.
        if (HttpContext.Items[CommunityProfileContentFinder.HandleItemKey] is not string handle)
        {
            return NotFound();
        }

        // A hidden profile is loaded only for its signed-in owner; everyone else gets null,
        // exactly as for a handle with no profile.
        var viewable = await _profileAccessService.GetViewableProfileAsync(handle, cancellationToken);
        var isOwnProfile = await _profileAccessService.IsOwnProfileAsync(handle);

        // Only an existing profile has sections to show.
        var access = viewable.Profile != null
            ? await _profileAccessService.ResolveAsync(currentPage, handle)
            : EnrichedSectionsAccess.None;

        var viewModel = new CommunityProfilePageViewModel(currentPage)
        {
            Handle = handle,
            Profile = viewable.Profile,
            IsOwnProfile = isOwnProfile,
            IsHiddenPreview = viewable.IsHiddenPreview,
            AccountPageUrl = viewable.IsHiddenPreview
                ? currentPage.GetSingletonPage<AccountPage>()?.Url(_publishedUrlProvider)
                : null,
            ShowEnrichedSections = access.Show,
            IsGated = access.IsGated,
        };

        return CurrentTemplate(viewModel);
    }
}

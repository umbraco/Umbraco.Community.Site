using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Web.Common.Controllers;
using UmbracoCommunity.Web.Features.Profiles;
using UmbracoCommunity.Web.Models.Pages;
using UmbracoCommunity.Web.Routing;

namespace UmbracoCommunity.Web.Controllers.Render;

public class CommunityProfilePageController : RenderController
{
    private readonly IProfileDataProvider _profileDataProvider;
    private readonly IMemberManager _memberManager;

    public CommunityProfilePageController(
        ILogger<CommunityProfilePageController> logger,
        ICompositeViewEngine compositeViewEngine,
        IUmbracoContextAccessor umbracoContextAccessor,
        IProfileDataProvider profileDataProvider,
        IMemberManager memberManager)
        : base(logger, compositeViewEngine, umbracoContextAccessor)
    {
        _profileDataProvider = profileDataProvider;
        _memberManager = memberManager;
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

        var profile = await _profileDataProvider.GetProfileAsync(handle, cancellationToken);

        var currentMember = await _memberManager.GetCurrentMemberAsync();
        var isOwnProfile = currentMember?.UserName != null
            && string.Equals(currentMember.UserName, handle, StringComparison.OrdinalIgnoreCase);

        var viewModel = new CommunityProfilePageViewModel(currentPage)
        {
            Handle = handle,
            Profile = profile,
            IsOwnProfile = isOwnProfile,
        };

        return CurrentTemplate(viewModel);
    }
}

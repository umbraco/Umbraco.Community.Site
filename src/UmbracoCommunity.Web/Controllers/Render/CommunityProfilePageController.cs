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
    private readonly ProfileActivityService _profileActivityService;
    private readonly ProfileTimelinePreviewService _timelinePreviewService;
    private readonly IPublishedUrlProvider _publishedUrlProvider;

    public CommunityProfilePageController(
        ILogger<CommunityProfilePageController> logger,
        ICompositeViewEngine compositeViewEngine,
        IUmbracoContextAccessor umbracoContextAccessor,
        ProfileAccessService profileAccessService,
        ProfileActivityService profileActivityService,
        ProfileTimelinePreviewService timelinePreviewService,
        IPublishedUrlProvider publishedUrlProvider)
        : base(logger, compositeViewEngine, umbracoContextAccessor)
    {
        _profileAccessService = profileAccessService;
        _profileActivityService = profileActivityService;
        _timelinePreviewService = timelinePreviewService;
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

        // A DevRel member looking at someone else's profile may ask for everything the member hid as well,
        // their hidden links included.
        var isDevRelViewer = await _profileAccessService.IsDevRelViewAsync(handle);
        var showEverything = isDevRelViewer && DevRelProfileView.IsShowEverythingRequested(Request.Query);

        // A hidden profile is loaded only for its signed-in owner and for DevRel; everyone else
        // gets null, exactly as for a handle with no profile.
        var viewable = await _profileAccessService.GetViewableProfileAsync(handle, includeHiddenLinks: showEverything, cancellationToken);
        var isOwnProfile = await _profileAccessService.IsOwnProfileAsync(handle);

        // Only an existing profile has sections to show.
        var access = viewable.Profile != null
            ? await _profileAccessService.ResolveAsync(currentPage, handle)
            : EnrichedSectionsAccess.None;

        // The DevRel page is never stored by a browser or shared cache.
        var isDevRelView = viewable.Profile != null && isDevRelViewer;
        if (isDevRelView)
        {
            DevRelProfileView.PreventCaching(Response);
        }

        // Activity is only loaded for a viewer who may see it; everyone else gets the page as it was.
        // The member's choices of what the sections show (sections off, hidden and pinned items) apply on top.
        var profile = viewable.Profile != null && access.Show
            ? ProfileDisplay.Apply(await _profileActivityService.EnrichAsync(viewable.Profile, cancellationToken), showHidden: showEverything)
            : viewable.Profile;

        // The timeline and the activity graph are built from the activity shown, so they sit behind the same gate.
        IReadOnlyList<ProfileTimelineEntry> timeline = [];
        ProfileActivityGraph? graph = null;
        if (profile != null && access.Show)
        {
            timeline = await _timelinePreviewService.AddCardsAsync(ProfileTimeline.Entries(profile), profile.DisplayChoices, cancellationToken);
            if (!profile.DisplayChoices.HideActivityGraph)
            {
                var built = ProfileActivityGraph.Build(timeline.Select(e => e.Date), DateOnly.FromDateTime(DateTime.UtcNow));
                graph = built.IsEmpty ? null : built;
            }
        }

        var timelineTypes = ProfileTimeline.Types(timeline);
        var viewModel = new CommunityProfilePageViewModel(currentPage)
        {
            Handle = handle,
            Profile = profile,
            IsOwnProfile = isOwnProfile,
            IsHiddenPreview = viewable.IsHiddenPreview,
            AccountPageUrl = isOwnProfile
                ? currentPage.GetSingletonPage<AccountPage>()?.Url(_publishedUrlProvider)
                : null,
            ShowEnrichedSections = access.Show,
            Timeline = ProfileTimeline.ByMonth(timeline),
            TimelineTypes = timelineTypes,
            TimelineType = ProfileTimeline.SelectedType(Request.Query[ProfileTimeline.TypeQueryKey].FirstOrDefault(), timelineTypes),
            ActivityGraph = graph,
            IsGated = access.IsGated,
            IsDevRelView = isDevRelView,
            IsHiddenFromPublic = viewable.IsHiddenFromPublic,
            HasHiddenSocialLinks = viewable.HasHiddenSocialLinks,
            IsShowingEverything = showEverything,
            DevRelPublicViewUrl = isDevRelView ? DevRelProfileView.PublicViewUrl(Request.Path) : null,
            DevRelShowEverythingUrl = isDevRelView ? DevRelProfileView.ShowEverythingUrl(Request.Path) : null,
        };

        return CurrentTemplate(viewModel);
    }
}

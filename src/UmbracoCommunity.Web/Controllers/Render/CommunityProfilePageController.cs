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

        // A hidden profile is loaded only for its signed-in owner; everyone else gets null,
        // exactly as for a handle with no profile.
        var viewable = await _profileAccessService.GetViewableProfileAsync(handle, cancellationToken);
        var isOwnProfile = await _profileAccessService.IsOwnProfileAsync(handle);

        // Only an existing profile has sections to show.
        var access = viewable.Profile != null
            ? await _profileAccessService.ResolveAsync(currentPage, handle)
            : EnrichedSectionsAccess.None;

        // Activity is only loaded for a viewer who may see it; everyone else gets the page as it was.
        // The member's choices of what the sections show (sections off, hidden and pinned items) apply on top.
        var profile = viewable.Profile != null && access.Show
            ? ProfileDisplay.Apply(await _profileActivityService.EnrichAsync(viewable.Profile, cancellationToken))
            : viewable.Profile;

        // The timeline and the activity graph are built from the activity shown, so they sit behind the same gate.
        IReadOnlyList<ProfileTimelineEntry> timeline = [];
        ProfileActivityGraph? graph = null;
        var isBuilding = profile is { JudgingProgress: not null } && access.Show;
        if (profile != null && access.Show && !isBuilding)
        {
            timeline = ProfileTimeline.Entries(profile);
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var built = ProfileActivityGraph.Build(timeline.Select(e => e.Date), today)
                with { Summary = ProfileActivityGraph.Summarise(timeline, today, profile.ForumStats, profile.ActivityCounts) };
            graph = built.IsEmpty ? null : built;
        }

        var timelineTypes = ProfileTimeline.Types(timeline);
        var timelineType = ProfileTimeline.SelectedType(Request.Query[ProfileTimeline.TypeQueryKey].FirstOrDefault(), timelineTypes);

        // Only the entries on the shown page get preview cards, so outside work is spent on what is actually in view.
        var timelinePage = ProfileTimeline.Paginate(timeline, timelineType, timelineTypes.Count > 1, Request.Query[ProfileTimeline.PageQueryKey].FirstOrDefault());

        // The Featured strip's pictures are found the same way as the timeline's cards, in the same batch (one wait,
        // one budget), Featured first as it is above the fold.
        var featured = profile != null ? ProfileFeatured.Highlights(profile) : [];
        var featuredLookups = ProfileFeatured.ImageLookups(featured);
        var timelineLookups = profile != null && access.Show ? timelinePage.Entries : [];
        IReadOnlyList<ProfileTimelineEntry> carded = featuredLookups.Count + timelineLookups.Count > 0
            ? await _timelinePreviewService.AddCardsAsync([.. featuredLookups, .. timelineLookups], cancellationToken)
            : [];
        featured = ProfileFeatured.WithImages(featured, carded.Take(featuredLookups.Count).ToList());
        var pageEntries = timelineLookups.Count > 0 ? carded.Skip(featuredLookups.Count).ToList() : timelinePage.Entries;
        var viewModel = new CommunityProfilePageViewModel(currentPage)
        {
            Handle = handle,
            Profile = profile,
            Featured = featured,
            IsOwnProfile = isOwnProfile,
            IsHiddenPreview = viewable.IsHiddenPreview,
            AccountPageUrl = isOwnProfile
                ? currentPage.GetSingletonPage<AccountPage>()?.Url(_publishedUrlProvider)
                : null,
            ShowEnrichedSections = access.Show,
            Timeline = ProfileTimeline.ByMonth(pageEntries),
            TimelineTypes = timelineTypes,
            TimelineType = timelineType,
            TimelinePage = timelinePage.Page,
            TimelinePageCount = timelinePage.PageCount,
            TimelineTotal = timelinePage.Total,
            ActivityGraph = graph,
            JudgingProgress = isBuilding ? profile!.JudgingProgress : null,
            IsGated = access.IsGated,
        };

        return CurrentTemplate(viewModel);
    }
}

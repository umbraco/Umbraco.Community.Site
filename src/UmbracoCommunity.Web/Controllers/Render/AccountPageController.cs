using Microsoft.AspNetCore.Http;
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
using UmbracoCommunity.Web.Features.OpenBadges;
using UmbracoCommunity.Web.Features.OpenBadges.Infrastructure;
using UmbracoCommunity.Web.Features.Profiles;
using UmbracoCommunity.Web.Features.Profiles.Data;
using UmbracoCommunity.Web.Features.Profiles.Data.Entities;
using UmbracoCommunity.Web.Models.Pages;
using UmbracoCommunity.Web.Models.PublishedModels;

namespace UmbracoCommunity.Web.Controllers.Render;

public class AccountPageController : RenderController
{
    private readonly IMemberManager _memberManager;
    private readonly MemberProfileStore _store;
    private readonly IPublishedUrlProvider _publishedUrlProvider;
    private readonly ProfileAvatarUrlResolver _avatarUrlResolver;
    private readonly IProfileSyncClient _profileSyncClient;
    private readonly GitHubProfileDefaultsService _gitHubDefaultsService;
    private readonly ProfileAccessService _profileAccessService;
    private readonly MvpBadgeService _mvpBadgeService;
    private readonly ProfileEditSubjectResolver _subjects;

    public AccountPageController(
        ILogger<AccountPageController> logger,
        ICompositeViewEngine compositeViewEngine,
        IUmbracoContextAccessor umbracoContextAccessor,
        IMemberManager memberManager,
        MemberProfileStore store,
        IPublishedUrlProvider publishedUrlProvider,
        ProfileAvatarUrlResolver avatarUrlResolver,
        IProfileSyncClient profileSyncClient,
        GitHubProfileDefaultsService gitHubDefaultsService,
        ProfileAccessService profileAccessService,
        MvpBadgeService mvpBadgeService,
        ProfileEditSubjectResolver subjects)
        : base(logger, compositeViewEngine, umbracoContextAccessor)
    {
        _memberManager = memberManager;
        _store = store;
        _publishedUrlProvider = publishedUrlProvider;
        _avatarUrlResolver = avatarUrlResolver;
        _profileSyncClient = profileSyncClient;
        _gitHubDefaultsService = gitHubDefaultsService;
        _profileAccessService = profileAccessService;
        _mvpBadgeService = mvpBadgeService;
        _subjects = subjects;
    }

    [NonAction]
    public sealed override IActionResult Index() => throw new NotImplementedException();

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (!_memberManager.IsLoggedIn())
            return Redirect("/");

        var currentPage = CurrentPage ?? throw new InvalidOperationException($"Cannot build view model as {nameof(CurrentPage)} is null.");

        // Whose account page this is: the signed-in member's own, or, for a profile admin, the member named in ?edit-as=.
        // Anyone else naming a member, or a handle without a profile, gets the tenant's not-found page.
        var requested = Request.Query.TryGetValue(ProfileEditSubjectResolver.QueryKey, out var editAs) ? editAs.ToString().Trim() : null;
        var resolved = await _subjects.ResolveAsync(requested, cancellationToken);
        if (resolved.Subject is not { } subject)
        {
            return resolved.Failure == ProfileEditSubjectFailure.NotSignedIn ? Redirect("/") : TenantNotFound(currentPage);
        }

        // On an edit-as request there may be no Umbraco member behind the profile, and the signed-in member's own
        // details (name, email) never stand in for it.
        var ownMember = subject.IsEditAs ? null : subject.SignedInMember;
        if (subject.IsEditAs)
        {
            Response.Headers.CacheControl = "private, no-store";
        }

        var handle = subject.Handle.Length > 0 ? subject.Handle : null;

        // Neither depends on the other's result (both only need the handle/member key, resolved
        // above), so they run concurrently rather than as two serialized round-trips.
        var profileTask = _store.GetByMemberKeyAsync(subject.MemberKey, cancellationToken);
        var lastSyncedTask = handle != null ? _profileSyncClient.GetLastSyncedAtAsync(handle, cancellationToken) : Task.FromResult<DateTimeOffset?>(null);
        await Task.WhenAll(profileTask, lastSyncedTask);

        var profile = profileTask.Result;
        var onboardingCompleted = profile?.OnboardingStatus == OnboardingStatus.Completed;

        // The profile details editor shows the GitHub bio and company as the defaults, so fetch
        // them now if onboarding didn't manage to.
        if (onboardingCompleted && profile != null)
        {
            await _gitHubDefaultsService.EnsureGitHubDefaultsAsync(profile, cancellationToken);
        }

        var viewModel = new AccountPageViewModel(currentPage)
        {
            DisplayName = profile?.EffectiveDisplayName ?? ownMember?.Name ?? handle,
            GitHubHandle = handle,
            Email = ownMember?.Email,
            AvatarUrl = handle != null ? _avatarUrlResolver.Resolve(profile?.AvatarMediaKey, handle) : null,
            OnboardingCompleted = onboardingCompleted,
            IsProfileVisible = profile?.IsProfileVisible ?? false,
            HasCustomAvatar = profile?.AvatarMediaKey.HasValue ?? false,
            CustomDisplayName = profile?.DisplayNameOverride,
            DefaultDisplayName = profile?.DisplayName ?? ownMember?.Name ?? handle,
            CustomHeadline = profile?.Headline,
            GitHubHeadline = profile?.GitHubCompany,
            CustomBio = profile?.Bio,
            GitHubBio = profile?.GitHubBio,
            CanChooseProfileDisplay = onboardingCompleted && await _profileAccessService.CanChooseProfileDisplayAsync(subject),
            CanAddActivity = onboardingCompleted && await _profileAccessService.CanAddActivityAsync(subject),
            CanMatchLinks = onboardingCompleted && await _profileAccessService.CanMatchLinksAsync(subject),
        };

        if (onboardingCompleted && handle != null)
        {
            var profilePage = currentPage.GetSingletonPage<CommunityProfilePage>();
            if (profilePage != null)
            {
                // Absolute so the account page can show/copy the real link (scheme + host
                // included) rather than a relative path.
                viewModel.ProfileUrl = $"{profilePage.Url(_publishedUrlProvider, mode: UrlMode.Absolute).TrimEnd('/')}/{handle}";
            }
        }
        else
        {
            var onboardingPage = currentPage.GetSingletonPage<OnboardingPage>();
            viewModel.OnboardingUrl = onboardingPage?.Url(_publishedUrlProvider);
        }

        viewModel.LastSyncedAt = lastSyncedTask.Result;

        // A profile admin editing someone else's profile sees what that member edits, without their email address or
        // the self-only sections (the MVP badge).
        if (subject.IsEditAs)
        {
            AccountPageEditAs.Apply(viewModel, subject);
            return CurrentTemplate(viewModel);
        }

        if (await _mvpBadgeService.GetDownloadableYearAsync(handle, cancellationToken) is { } mvpBadgeYear)
        {
            viewModel.MvpBadgeYear = mvpBadgeYear;
            viewModel.MvpBadgeDownloadUrl = OpenBadgesRoutes.MvpDownloadPath(mvpBadgeYear);
            viewModel.MvpBadgeImageUrl = OpenBadgesRoutes.MvpImagePath(mvpBadgeYear);
        }

        return CurrentTemplate(viewModel);
    }

    // The tenant's normal not-found page, as a 404.
    private IActionResult TenantNotFound(IPublishedContent currentPage)
    {
        if (currentPage.GetSingletonPage<PageNotFound>() is not { } notFoundPage)
        {
            return NotFound();
        }

        var view = View("~/Views/PageNotFound.cshtml", notFoundPage);
        view.StatusCode = StatusCodes.Status404NotFound;
        return view;
    }
}

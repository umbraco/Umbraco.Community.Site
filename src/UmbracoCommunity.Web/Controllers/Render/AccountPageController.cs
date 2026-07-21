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

    public AccountPageController(
        ILogger<AccountPageController> logger,
        ICompositeViewEngine compositeViewEngine,
        IUmbracoContextAccessor umbracoContextAccessor,
        IMemberManager memberManager,
        MemberProfileStore store,
        IPublishedUrlProvider publishedUrlProvider,
        ProfileAvatarUrlResolver avatarUrlResolver)
        : base(logger, compositeViewEngine, umbracoContextAccessor)
    {
        _memberManager = memberManager;
        _store = store;
        _publishedUrlProvider = publishedUrlProvider;
        _avatarUrlResolver = avatarUrlResolver;
    }

    [NonAction]
    public sealed override IActionResult Index() => throw new NotImplementedException();

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (!_memberManager.IsLoggedIn())
            return Redirect("/");

        var currentPage = CurrentPage ?? throw new InvalidOperationException($"Cannot build view model as {nameof(CurrentPage)} is null.");
        var member = await _memberManager.GetCurrentMemberAsync();

        var handle = member?.UserName;
        var profile = member != null ? await _store.GetByMemberKeyAsync(member.Key, cancellationToken) : null;
        var onboardingCompleted = profile?.OnboardingStatus == OnboardingStatus.Completed;

        var viewModel = new AccountPageViewModel(currentPage)
        {
            DisplayName = member?.Name,
            GitHubHandle = handle,
            Email = member?.Email,
            AvatarUrl = handle != null ? _avatarUrlResolver.Resolve(profile?.AvatarMediaKey, handle) : null,
            OnboardingCompleted = onboardingCompleted,
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

        return CurrentTemplate(viewModel);
    }
}

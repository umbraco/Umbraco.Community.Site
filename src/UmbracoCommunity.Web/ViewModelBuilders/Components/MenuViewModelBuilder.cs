using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Models.Blocks;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;
using UmbracoCommunity.Web.Extensions;
using UmbracoCommunity.Web.Features.Profiles;
using UmbracoCommunity.Web.Features.Profiles.Data;
using UmbracoCommunity.Web.Models.PublishedModels;
using UmbracoCommunity.Web.Models.ViewModels.Components;
using UmbracoCommunity.Web.Models.ViewModels.Components.Navigation;

namespace UmbracoCommunity.Web.ViewModelBuilders.Components;

internal class MenuViewModelBuilder : IAsyncViewModelBuilder<MenuViewModel>
{
    private readonly IPublishedUrlProvider _publishedUrlProvider;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IMemberManager _memberManager;
    private readonly MemberProfileStore _memberProfileStore;
    private readonly ProfileAvatarUrlResolver _avatarUrlResolver;
    private readonly ILogger<MenuViewModelBuilder> _logger;

    public MenuViewModelBuilder(
        IPublishedContentQuery publishedContentQuery,
        IPublishedUrlProvider publishedUrlProvider,
        AppCaches appCaches,
        IHttpContextAccessor httpContextAccessor,
        IMemberManager memberManager,
        MemberProfileStore memberProfileStore,
        ProfileAvatarUrlResolver avatarUrlResolver,
        ILogger<MenuViewModelBuilder> logger)
    {
        _publishedUrlProvider = publishedUrlProvider;
        _httpContextAccessor = httpContextAccessor;
        _memberManager = memberManager;
        _memberProfileStore = memberProfileStore;
        _avatarUrlResolver = avatarUrlResolver;
        _logger = logger;
    }

    public async Task<MenuViewModel> Build(IPublishedContent currentPage, IUmbracoContext umbracoContext)
    {
        return await CreateViewModelAndPopulateTopLevelNavigation(currentPage);
    }

    /// <summary>
    /// Fills in the signed-in member's details. Does nothing (and looks nothing up) unless
    /// sign-in is enabled for the site and a member is signed in.
    /// </summary>
    internal async Task PopulateMemberAsync(MenuViewModel viewModel, bool signInEnabled)
    {
        if (!signInEnabled || !_memberManager.IsLoggedIn())
        {
            return;
        }

        viewModel.IsSignedIn = true;
        var user = _httpContextAccessor.HttpContext?.User;
        var handle = user?.Identity?.Name;
        viewModel.MemberDisplayName = user?.FindFirst(System.Security.Claims.ClaimTypes.GivenName)?.Value ?? handle;
        viewModel.MemberAvatarUrl = handle != null ? await ResolveMemberAvatarUrlAsync(handle) : null;
    }

    // Same rule as the account and profile pages: an uploaded avatar first, otherwise the
    // GitHub avatar. Any failure falls back to the GitHub avatar so the header always renders.
    private async Task<string> ResolveMemberAvatarUrlAsync(string handle)
    {
        var gitHubAvatarUrl = $"https://github.com/{handle}.png";
        try
        {
            var member = await _memberManager.GetCurrentMemberAsync();
            if (member == null)
            {
                return gitHubAvatarUrl;
            }

            var profile = await _memberProfileStore.GetByMemberKeyAsync(member.Key);
            return _avatarUrlResolver.Resolve(profile?.AvatarMediaKey, handle);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not resolve the signed-in member's avatar; using the GitHub avatar.");
            return gitHubAvatarUrl;
        }
    }

    private async Task<MenuViewModel> CreateViewModelAndPopulateTopLevelNavigation(IPublishedContent currentPage)
    {
        MenuViewModel viewModel = new();

        var siteSettings = currentPage.GetSiteSettings();
        viewModel.SiteName = siteSettings?.SiteName;
        viewModel.Logo = siteSettings?.HeaderLogo;

        var signInEnabled = siteSettings?.EnableMemberSignIn ?? false;
        viewModel.IsSignInEnabled = signInEnabled;

        await PopulateMemberAsync(viewModel, signInEnabled);

        var searchPage = currentPage.GetSingletonPage<SearchPage>();
        if (searchPage != null)
        {
            viewModel.SearchPageUrl = searchPage.Url(_publishedUrlProvider);
        }

        if (viewModel.IsSignedIn)
        {
            var accountPage = currentPage.GetSingletonPage<AccountPage>();
            if (accountPage != null)
            {
                viewModel.AccountPageUrl = accountPage.Url(_publishedUrlProvider);
            }
        }

        var navSettings = currentPage.GetNavigationSettings(siteSettings);
        if (navSettings?.HeaderNavigationItems == null || !navSettings.HeaderNavigationItems.Any()) return viewModel;

        foreach (var navItem in navSettings.HeaderNavigationItems)
        {
            switch (navItem.Content.ContentType.Alias)
            {
                case LinkItem.ModelTypeAlias:
                    var link = navItem.Content as LinkItem;
                    if (link?.Link != null)
                    {
                        var navLink = new NavigationItem(link.Link);
                        if (!string.IsNullOrWhiteSpace(link.LinkTitle))
                        {
                            navLink.Text = link.LinkTitle;
                        }
                        viewModel.AddTopLevelNavigationItem(navLink);
                    }
                    break;
                case NavigationSection.ModelTypeAlias:
                    var linkSection = navItem.Content as NavigationSection;
                    if (linkSection != null)
                    {
                        var section = new NavigationMenuItem(linkSection?.SectionName ?? "Title");

                        if (linkSection?.Columns != null && linkSection.Columns.Any())
                        {
                            foreach (var col in linkSection.Columns)
                            {
                                if (col != null)
                                {
                                    var column = col as BlockListItem<NavigationSectionColumn>;

                                    var columnArea = new NavigationSectionArea
                                    {
                                        Headline = column?.Content.Headline
                                    };

                                    if (column?.Content?.NavigationColumnItems == null) continue;
                                    foreach (var item in column.Content.NavigationColumnItems)
                                    {
                                        var linkItem = item.Content as LinkWithIconAndCaption;
                                        if (linkItem?.Link == null) continue;
                                        var newLink = new NavigationLink(linkItem.Link)
                                        {
                                            Caption = linkItem.Caption,
                                            IconUrl = linkItem.Icon != null ? linkItem.Icon.GetCropUrl() : null
                                        };
                                        if (!string.IsNullOrWhiteSpace(linkItem.LinkTitle))
                                        {
                                            newLink.Text = linkItem.LinkTitle;
                                        }
                                        columnArea.AddLink(newLink);
                                    }

                                    section.AddSection(columnArea);
                                }
                            }
                        }

                        viewModel.AddTopLevelNavigationItem(section);
                    }
                    break;
                default:
                    continue;
            }
        }

        if (navSettings.CallToActionButton != null)
        {
            viewModel.CallToActionButton = new NavigationLink(navSettings.CallToActionButton);
        }

        return viewModel;
    }
}

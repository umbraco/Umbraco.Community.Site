using Microsoft.AspNetCore.Http;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Models.Blocks;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;
using UmbracoCommunity.Web.Extensions;
using UmbracoCommunity.Web.Models.PublishedModels;
using UmbracoCommunity.Web.Models.ViewModels.Components;
using UmbracoCommunity.Web.Models.ViewModels.Components.Navigation;

namespace UmbracoCommunity.Web.ViewModelBuilders.Components;

internal class MenuViewModelBuilder : IViewModelBuilder<MenuViewModel>
{
    private const string SearchPageContentTypeAlias = "searchPage";

    private readonly IPublishedUrlProvider _publishedUrlProvider;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public MenuViewModelBuilder(IPublishedContentQuery publishedContentQuery, IPublishedUrlProvider publishedUrlProvider, AppCaches appCaches, IHttpContextAccessor httpContextAccessor)
    {
        _publishedUrlProvider = publishedUrlProvider;
        _httpContextAccessor = httpContextAccessor;
    }

    public MenuViewModel Build(IPublishedContent currentPage, IUmbracoContext umbracoContext)
    {
        return CreateViewModelAndPopulateTopLevelNavigation(currentPage);
    }

    private MenuViewModel CreateViewModelAndPopulateTopLevelNavigation(IPublishedContent currentPage)
    {
        MenuViewModel viewModel = new();

        var siteSettings = currentPage.GetSiteSettings();
        viewModel.SiteName = siteSettings?.SiteName;
        viewModel.Logo = siteSettings?.HeaderLogo;

        var signInEnabled = siteSettings?.EnableMemberSignIn ?? false;
        viewModel.IsSignInEnabled = signInEnabled;

        if (signInEnabled && _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated == true)
        {
            viewModel.IsSignedIn = true;
            var user = _httpContextAccessor.HttpContext?.User;
            var handle = user?.Identity?.Name;
            viewModel.MemberDisplayName = user?.FindFirst(System.Security.Claims.ClaimTypes.GivenName)?.Value ?? handle;
            viewModel.MemberAvatarUrl = handle != null ? $"https://github.com/{handle}.png" : null;
        }

        var searchPage = currentPage.Root()
            .DescendantsOrSelf()
            .FirstOrDefault(c => c.ContentType.Alias.Equals(SearchPageContentTypeAlias, StringComparison.OrdinalIgnoreCase));
        if (searchPage != null)
        {
            viewModel.SearchPageUrl = searchPage.Url(_publishedUrlProvider);
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

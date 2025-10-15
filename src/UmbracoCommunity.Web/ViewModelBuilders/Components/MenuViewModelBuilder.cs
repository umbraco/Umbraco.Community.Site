using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Media;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Blocks;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;
using UmbracoCommunity.Web.Extensions;
using UmbracoCommunity.Web.Models.PublishedModels;
using UmbracoCommunity.Web.Models.ViewModels.Components;
using UmbracoCommunity.Web.Models.ViewModels.Components.Navigation;

namespace UmbracoCommunity.Web.ViewModelBuilders.Components;

internal class MenuViewModelBuilder : NavigationViewModelBuilderBase, IViewModelBuilder<MenuViewModel>
{
    private readonly IPublishedUrlProvider _publishedUrlProvider;
    private readonly IImageUrlGenerator _imageUrlGenerator;
    private readonly IPublishedValueFallback _publishedValueFallback;
    private readonly IFileService _fileService;

    public MenuViewModelBuilder(
        IPublishedContentQuery publishedContentQuery,
        IPublishedUrlProvider publishedUrlProvider,
        IImageUrlGenerator imageUrlGenerator,
        IPublishedValueFallback publishedValueFallback,
        IFileService fileService,
        AppCaches appCaches)
        : base(publishedContentQuery, publishedUrlProvider, appCaches)
    {
        _publishedUrlProvider = publishedUrlProvider;
        _imageUrlGenerator = imageUrlGenerator;
        _publishedValueFallback = publishedValueFallback;
        _fileService = fileService;
    }

    public MenuViewModel Build(IPublishedContent currentPage, IUmbracoContext umbracoContext)
    {
        return CreateViewModelAndPopulateTopLevelNavigation(currentPage);
    }

    private MenuViewModel CreateViewModelAndPopulateTopLevelNavigation(IPublishedContent currentPage)
    {
        MenuViewModel viewModel = new();

        var navSettings = currentPage.GetNavigationSettings();
        if (navSettings?.HeaderNavigationItems == null || !navSettings.HeaderNavigationItems.Any()) return viewModel;

        foreach (var navItem in navSettings.HeaderNavigationItems)
        {
            switch (navItem.Content.ContentType.Alias)
            {
                case LinkItem.ModelTypeAlias:
                    var link = navItem.Content as LinkItem;
                    if (link?.Link != null)
                    {
                        viewModel.AddTopLevelNavigationItem(new NavigationItem(link.Link));
                    }
                    break;
                case NavigationSection.ModelTypeAlias:
                    var linkSection = navItem.Content as NavigationSection;
                    if (linkSection != null)
                    {
                        var section = new NavigationMenuItem(linkSection?.SectionName ?? "Title");

                        if (linkSection.Columns != null && linkSection.Columns.Any())
                        {
                            foreach (var col in linkSection.Columns)
                            {
                                var columnArea = new NavigationSectionArea
                                {
                                    Headline = linkSection.SectionName
                                };
                                if (col != null)
                                {
                                    var column = col as BlockListItem<NavigationSectionColumn>;
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
                                        columnArea.AddLink(newLink);
                                    }
                                }
                                section.AddSection(columnArea);
                            }
                        }

                        viewModel.AddTopLevelNavigationItem(section);
                    }
                    break;
                default:
                    continue;
            }
        }

        viewModel.AddCtaButton(new Link { Name = "Sign in", Url = "login" });

        return viewModel;
    }
}

using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Media;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;
using UmbracoCommunity.Web.Models.ViewModels.Components;
using static UmbracoCommunity.Web.Models.ViewModels.Components.MenuViewModel;

namespace UmbracoCommunity.Web.ViewModelBuilders.Components;

internal class MenuReleasesViewModelBuilder : NavigationViewModelBuilderBase, IViewModelBuilder<MenuReleasesViewModel>
{
    private readonly IPublishedUrlProvider _publishedUrlProvider;
    private readonly IImageUrlGenerator _imageUrlGenerator;
    private readonly IPublishedValueFallback _publishedValueFallback;
    private readonly IFileService _fileService;

    public MenuReleasesViewModelBuilder(
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

    public MenuReleasesViewModel Build(IPublishedContent currentPage, IUmbracoContext umbracoContext)
    {
        return CreateViewModelAndPopulateTopLevelNavigation(currentPage);
    }

    private MenuReleasesViewModel CreateViewModelAndPopulateTopLevelNavigation(IPublishedContent currentPage)
    {
        MenuReleasesViewModel viewModel = new();

        viewModel.AddTopLevelNavigationItem(new NavigationItem(new Link { Name = "Latest", Url = "/" }));
        viewModel.AddTopLevelNavigationItem(new NavigationItem(new Link { Name = "Compare", Url = "/compare" }));
        viewModel.AddTopLevelNavigationItem(new NavigationItem(new Link { Name = "All releases", Url = "/all-releases" }));

        return viewModel;
    }
}

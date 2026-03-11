using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;
using UmbracoCommunity.Web.Models.Pages;
using UmbracoCommunity.Web.Models.PublishedModels;

namespace UmbracoCommunity.Web.ViewModelBuilders.Pages
{
    internal class HomePageViewModelBuilder : ViewModelBuilderBase, IViewModelBuilder<HomePageViewModel>
    {
        public HomePageViewModel Build(IPublishedContent currentPage, IUmbracoContext umbracoContext)
        {
            var bannerContent = currentPage as ICompositionBannerBlock
                ?? throw new InvalidOperationException($"Content type '{currentPage.ContentType.Alias}' does not implement {nameof(ICompositionBannerBlock)}.");
            var contentBlocks = currentPage as ICompositionContentBlocks
                ?? throw new InvalidOperationException($"Content type '{currentPage.ContentType.Alias}' does not implement {nameof(ICompositionContentBlocks)}.");

            var viewModel = new HomePageViewModel(currentPage)
            {
                BlockContent = ParseBlockGrid(contentBlocks.ContentBlocks),
                Banner = bannerContent.BannerContent
            };

            return viewModel;
        }
    }
}

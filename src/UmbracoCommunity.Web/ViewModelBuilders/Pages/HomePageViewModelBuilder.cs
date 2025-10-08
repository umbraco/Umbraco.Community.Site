using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;
using UmbracoCommunity.Web.Extensions;
using UmbracoCommunity.Web.Models.Pages;
using UmbracoCommunity.Web.Models.PublishedModels;

namespace UmbracoCommunity.Web.ViewModelBuilders.Pages
{
    internal class HomePageViewModelBuilder : ViewModelBuilderBase, IViewModelBuilder<HomePageViewModel>
    {
        public HomePageViewModel Build(IPublishedContent currentPage, IUmbracoContext umbracoContext)
        {
            Home contentModel = currentPage.As<Home>();

            var viewModel = new HomePageViewModel(currentPage)
            {
                BlockContent = ParseBlockGrid(contentModel.ContentBlocks)
            };

            return viewModel;
        }
    }
}

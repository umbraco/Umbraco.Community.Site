using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;
using UmbracoCommunity.Web.Extensions;
using UmbracoCommunity.Web.Models.Pages;
using UmbracoCommunity.Web.Models.PublishedModels;

namespace UmbracoCommunity.Web.ViewModelBuilders.Pages
{
    internal class ReleasesHomePageViewModelBuilder : ViewModelBuilderBase, IViewModelBuilder<ReleasesHomePageViewModel>
    {
        public ReleasesHomePageViewModel Build(IPublishedContent currentPage, IUmbracoContext umbracoContext)
        {
            ReleasesHome contentModel = currentPage.As<ReleasesHome>();

            var viewModel = new ReleasesHomePageViewModel(currentPage)
            {
                BlockContent = ParseBlockGrid(contentModel.ContentBlocks)
            };

            return viewModel;
        }
    }
}

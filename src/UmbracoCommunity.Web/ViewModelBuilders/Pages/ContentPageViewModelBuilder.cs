using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;
using UmbracoCommunity.Web.Extensions;
using UmbracoCommunity.Web.Models.Pages;
using UmbracoCommunity.Web.Models.PublishedModels;

namespace UmbracoCommunity.Web.ViewModelBuilders.Pages
{
    internal class ContentPageViewModelBuilder : ViewModelBuilderBase, IViewModelBuilder<ContentPageViewModel>
    {
        public ContentPageViewModel Build(IPublishedContent currentPage, IUmbracoContext umbracoContext)
        {
            ContentPage contentModel = currentPage.As<ContentPage>();

            var viewModel = new ContentPageViewModel(currentPage)
            {
                BlockContent = contentModel.ContentBlocks.ParseBlockGrid(),
                Banner = contentModel.BannerContent
            };

            return viewModel;
        }
    }
}

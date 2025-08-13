using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;
using UmbracoCommunity.Web.Extensions;
using UmbracoCommunity.Web.Models.Pages.Testing;
using UmbracoCommunity.Web.Models.PublishedModels;

namespace UmbracoCommunity.Web.ViewModelBuilders.Pages
{
    internal class TestHomePageViewModelBuilder : ViewModelBuilderBase, IViewModelBuilder<TestHomePageViewModel>
    {
        public TestHomePageViewModel Build(IPublishedContent currentPage, IUmbracoContext umbracoContext)
        {
            TestHome contentModel = currentPage.As<TestHome>();

            var viewModel = new TestHomePageViewModel(currentPage);
            viewModel.MediaItemUrl = contentModel.MediaItem is not null
                ? contentModel.MediaItem.GetCropUrl()
                : null;

            return viewModel;
        }
    }
}

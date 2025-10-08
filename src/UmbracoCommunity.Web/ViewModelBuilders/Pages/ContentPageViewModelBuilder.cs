using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;
using UmbracoCommunity.Web.Models.Pages;

namespace UmbracoCommunity.Web.ViewModelBuilders.Pages
{
    internal class ContentPageViewModelBuilder : ViewModelBuilderBase, IViewModelBuilder<ContentPageViewModel>
    {
        public ContentPageViewModel Build(IPublishedContent currentPage, IUmbracoContext umbracoContext)
        {
            return new ContentPageViewModel(currentPage);
        }
    }
}

using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;
using UmbracoCommunity.Web.Models.Pages;

namespace UmbracoCommunity.Web.ViewModelBuilders.Pages
{
    internal class BlogPageViewModelBuilder : ViewModelBuilderBase, IViewModelBuilder<BlogPageViewModel>
    {
        public BlogPageViewModel Build(IPublishedContent currentPage, IUmbracoContext umbracoContext)
        {
            return new BlogPageViewModel(currentPage);
        }
    }
}

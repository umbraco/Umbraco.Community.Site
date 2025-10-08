using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;
using UmbracoCommunity.Web.Models.Pages;

namespace UmbracoCommunity.Web.ViewModelBuilders.Pages
{
    internal class ArticlePageViewModelBuilder : ViewModelBuilderBase, IViewModelBuilder<ArticlePageViewModel>
    {
        public ArticlePageViewModel Build(IPublishedContent currentPage, IUmbracoContext umbracoContext)
        {
            return new ArticlePageViewModel(currentPage);
        }
    }
}

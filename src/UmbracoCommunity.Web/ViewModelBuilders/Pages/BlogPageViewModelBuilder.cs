using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;
using UmbracoCommunity.Web.Extensions;
using UmbracoCommunity.Web.Models.Pages;
using UmbracoCommunity.Web.Models.PublishedModels;

namespace UmbracoCommunity.Web.ViewModelBuilders.Pages
{
    internal class BlogPageViewModelBuilder : ViewModelBuilderBase, IViewModelBuilder<BlogPageViewModel>
    {
        public BlogPageViewModel Build(IPublishedContent currentPage, IUmbracoContext umbracoContext)
        {
            Blog contentModel = currentPage.As<Blog>();

            return new BlogPageViewModel(currentPage)
            {
                PageSize = contentModel.NumberOfBlogArticlesPerPage
            };
        }
    }
}

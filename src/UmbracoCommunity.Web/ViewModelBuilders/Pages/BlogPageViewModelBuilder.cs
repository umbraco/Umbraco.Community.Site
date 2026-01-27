using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;
using UmbracoCommunity.Web.Extensions;
using UmbracoCommunity.Web.Models.Pages;
using UmbracoCommunity.Web.Models.PublishedModels;
using UmbracoCommunity.Web.Models.ViewModels.Components;

namespace UmbracoCommunity.Web.ViewModelBuilders.Pages
{
    internal class BlogPageViewModelBuilder : ViewModelBuilderBase, IViewModelBuilder<BlogPageViewModel>
    {
        public BlogPageViewModel Build(IPublishedContent currentPage, IUmbracoContext umbracoContext)
        {
            Blog contentModel = currentPage.As<Blog>();

            var viewModel = new BlogPageViewModel(currentPage)
            {
                RssPath = contentModel.RssPath
            };

            // Map featured blog post if set
            if (contentModel.FeaturedBlogPost != null)
            {
                viewModel.FeaturedBlogPost = MapToBlogPostCard(contentModel.FeaturedBlogPost);
            }

            // Get child articles and map to cards
            var articles = currentPage.Children<Article>()
                .OrderByDescending(a => a.PublishDate != default ? a.PublishDate : a.CreateDate)
                .ToList();

            viewModel.BlogPosts = articles
                .Where(a => a.Id != contentModel.FeaturedBlogPost?.Id)
                .Select(MapToBlogPostCard)
                .ToList();

            return viewModel;
        }

        private static BlogPostCardViewModel MapToBlogPostCard(IPublishedContent content)
        {
            var article = content.As<Article>();

            return new BlogPostCardViewModel
            {
                Title = content.Name,
                Url = content.Url() ?? string.Empty,
                Teaser = article.Teaser?.ToHtmlString(),
                PublishDate = article.PublishDate != default ? article.PublishDate : content.CreateDate,
                ReadTime = article.ReadTime
            };
        }
    }
}

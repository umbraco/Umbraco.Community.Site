using Umbraco.Cms.Core.Models.PublishedContent;
using UmbracoCommunity.Web.Models.ViewModels.Components;

namespace UmbracoCommunity.Web.Models.Pages
{
    public class BlogPageViewModel(IPublishedContent currentPage) : PageViewModelBase(currentPage)
    {
        public string? RssPath { get; set; }

        public bool HasFeaturedBlogPost => FeaturedBlogPost != null;

        public BlogPostCardViewModel? FeaturedBlogPost { get; set; }

        public IList<BlogPostCardViewModel> BlogPosts { get; set; } = [];

        public PagingViewModel Paging { get; set; } = new();

        public IList<CategoryViewModel> Categories { get; set; } = [];

        public IList<string> Tags { get; set; } = [];
    }
}

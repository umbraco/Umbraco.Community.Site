using Umbraco.Cms.Core.Models.PublishedContent;
using UmbracoCommunity.Web.Models.PublishedModels;

namespace UmbracoCommunity.Web.Services;

public interface IBlogService
{
    IEnumerable<Article> GetRecentArticles(
        IPublishedContent? currentPage,
        IEnumerable<Guid>? categoryKeys = null,
        IEnumerable<string>? tags = null,
        int count = 3);

    Blog? GetBlogPage(IPublishedContent? currentPage);
}

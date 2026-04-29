using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Extensions;
using UmbracoCommunity.Web.Models.PublishedModels;

namespace UmbracoCommunity.Web.Services;

internal class BlogService : IBlogService
{
    public Blog? GetBlogPage(IPublishedContent? currentPage)
        => currentPage?.Root()?.DescendantsOrSelf<Blog>().FirstOrDefault();

    public IEnumerable<Article> GetRecentArticles(
        IPublishedContent? currentPage,
        IEnumerable<Guid>? categoryKeys = null,
        IEnumerable<string>? tags = null,
        int count = 3)
    {
        if (count <= 0)
        {
            return [];
        }

        var blogPage = GetBlogPage(currentPage);
        if (blogPage is null)
        {
            return [];
        }

        IEnumerable<Article> articles = blogPage.Descendants<Article>()
            .OrderByDescending(a => a.PublishDate != default ? a.PublishDate : a.CreateDate);

        var categoryKeySet = (categoryKeys ?? []).ToHashSet();
        if (categoryKeySet.Count > 0)
        {
            articles = articles.Where(a =>
                a.Categories != null && a.Categories.Any(c => categoryKeySet.Contains(c.Key)));
        }

        var tagSet = (tags ?? [])
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (tagSet.Count > 0)
        {
            articles = articles.Where(a =>
                a.Tags != null && a.Tags.Any(t => tagSet.Contains(t)));
        }

        return articles.Take(count);
    }
}

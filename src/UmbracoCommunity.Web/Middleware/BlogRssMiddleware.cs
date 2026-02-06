using System.Xml.Linq;
using Microsoft.AspNetCore.Http;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;
using UmbracoCommunity.Web.Extensions;
using UmbracoCommunity.Web.Models.PublishedModels;

namespace UmbracoCommunity.Web.Middleware;

public class BlogRssMiddleware
{
    private readonly RequestDelegate _next;

    public BlogRssMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IUmbracoContextFactory umbracoContextFactory, IPublishedContentQuery publishedContentQuery)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        if (!path.EndsWith("/rss", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        // Strip /rss to get the parent content path
        var parentPath = path[..^4].TrimEnd('/');

        using var umbracoContextReference = umbracoContextFactory.EnsureUmbracoContext();

        // Find a Blog node whose URL matches the parent path
        var blogPage = publishedContentQuery.ContentAtRoot()
            .SelectMany(r => r.DescendantsOrSelf<Blog>())
            .FirstOrDefault(b =>
                (b.Url(mode: UrlMode.Relative)?.TrimEnd('/') ?? "").Equals(parentPath, StringComparison.OrdinalIgnoreCase));

        if (blogPage == null)
        {
            await _next(context);
            return;
        }

        await WriteRssFeed(context, blogPage);
    }

    private static async Task WriteRssFeed(HttpContext context, Blog blogPage)
    {
        var request = context.Request;
        var itemCount = blogPage.RssItemCount > 0 ? blogPage.RssItemCount : 25;
        var feedTitle = !string.IsNullOrWhiteSpace(blogPage.RssTitle) ? blogPage.RssTitle : blogPage.Name ?? "Blog";
        var feedDescription = !string.IsNullOrWhiteSpace(blogPage.RssDescription) ? blogPage.RssDescription : $"The latest articles from {feedTitle}";

        var articles = blogPage.Descendants<Article>()
            .OrderByDescending(a => a.PublishDate != default ? a.PublishDate : a.CreateDate)
            .Take(itemCount)
            .ToList();

        var blogUrl = $"{request.Scheme}://{request.Host}{blogPage.Url()}";
        var feedUrl = $"{request.Scheme}://{request.Host}{request.Path}";

        XNamespace atom = "http://www.w3.org/2005/Atom";
        XNamespace dc = "http://purl.org/dc/elements/1.1/";

        var items = articles.Select(article =>
        {
            var articleDate = DateTime.SpecifyKind(
                article.PublishDate != default ? article.PublishDate : article.CreateDate,
                DateTimeKind.Utc);
            var articleUrl = $"{request.Scheme}://{request.Host}{article.Url()}";

            var item = new XElement("item",
                new XElement("title", article.Name),
                new XElement("link", articleUrl),
                new XElement("guid", new XAttribute("isPermaLink", "true"), articleUrl),
                new XElement("pubDate", articleDate.ToString("R")));

            if (article.Teaser is not null)
            {
                item.Add(new XElement("description", article.Teaser.ToHtmlString()));
            }

            var author = article.Author as Author;
            if (author is not null)
            {
                item.Add(new XElement(dc + "creator", author.Name));
            }

            if (article.Categories is not null)
            {
                foreach (var cat in article.Categories)
                {
                    item.Add(new XElement("category", cat.Name));
                }
            }

            if (article.Tags is not null)
            {
                foreach (var tag in article.Tags)
                {
                    item.Add(new XElement("category", tag));
                }
            }

            return item;
        });

        var rss = new XElement("rss",
            new XAttribute("version", "2.0"),
            new XAttribute(XNamespace.Xmlns + "atom", atom),
            new XAttribute(XNamespace.Xmlns + "dc", dc),
            new XElement("channel",
                new XElement("title", feedTitle),
                new XElement("link", blogUrl),
                new XElement("description", feedDescription),
                new XElement("language", "en"),
                new XElement(atom + "link",
                    new XAttribute("href", feedUrl),
                    new XAttribute("rel", "self"),
                    new XAttribute("type", "application/rss+xml")),
                items));

        var doc = new XDocument(new XDeclaration("1.0", "utf-8", null), rss);

        context.Response.ContentType = "application/rss+xml";
        context.Response.Headers.CacheControl = "public, max-age=3600";
        context.Response.StatusCode = 200;
        await context.Response.WriteAsync(doc.ToString());
    }
}

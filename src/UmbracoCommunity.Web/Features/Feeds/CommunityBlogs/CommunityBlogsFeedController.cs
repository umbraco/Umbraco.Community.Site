using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Microsoft.AspNetCore.Mvc;

namespace UmbracoCommunity.Web.Features.Feeds.CommunityBlogs;

/// <summary>Serves the aggregated community blog posts as a global RSS feed at /feeds/community-blogs.xml.</summary>
public partial class CommunityBlogsFeedController : Controller
{
    private readonly ICommunityBlogsService _communityBlogsService;

    public CommunityBlogsFeedController(ICommunityBlogsService communityBlogsService)
    {
        _communityBlogsService = communityBlogsService;
    }

    public IActionResult Index()
    {
        var data = _communityBlogsService.GetData();

        var siteUrl = $"{Request.Scheme}://{Request.Host}";
        var feedUrl = $"{siteUrl}{Request.Path}";

        XNamespace atom = "http://www.w3.org/2005/Atom";
        XNamespace dc = "http://purl.org/dc/elements/1.1/";

        var items = data.Posts.Select(post =>
        {
            var item = new XElement("item",
                new XElement("title", post.Title),
                new XElement("link", post.Url),
                new XElement("guid", new XAttribute("isPermaLink", "false"), post.Id),
                new XElement("pubDate", post.PublishedAt.UtcDateTime.ToString("R")));

            if (!string.IsNullOrWhiteSpace(post.Excerpt))
            {
                item.Add(new XElement("description", StripHtmlTags(post.Excerpt)));
            }

            if (!string.IsNullOrWhiteSpace(post.AuthorName))
            {
                item.Add(new XElement(dc + "creator", post.AuthorName));
            }

            if (!string.IsNullOrWhiteSpace(post.CoverImageUrl))
            {
                var enclosureUrl = post.CoverImageUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                    ? post.CoverImageUrl
                    : $"{siteUrl}{post.CoverImageUrl}";

                item.Add(new XElement("enclosure",
                    new XAttribute("url", enclosureUrl),
                    new XAttribute("type", "image/jpeg")));
            }

            return item;
        });

        var rss = new XElement("rss",
            new XAttribute("version", "2.0"),
            new XAttribute(XNamespace.Xmlns + "atom", atom),
            new XAttribute(XNamespace.Xmlns + "dc", dc),
            new XElement("channel",
                new XElement("title", "Umbraco Community Blogs"),
                new XElement("link", siteUrl),
                new XElement("description", "Recent blog posts from members of the Umbraco community."),
                new XElement("language", "en"),
                new XElement(atom + "link",
                    new XAttribute("href", feedUrl),
                    new XAttribute("rel", "self"),
                    new XAttribute("type", "application/rss+xml")),
                items));

        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XProcessingInstruction("xml-stylesheet", "type=\"text/xsl\" href=\"/rss.xsl\""),
            rss);

        using var stream = new MemoryStream();
        using (var xmlWriter = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new UTF8Encoding(false) }))
        {
            doc.Save(xmlWriter);
        }

        Response.Headers.CacheControl = "public, max-age=900";

        return File(stream.ToArray(), "application/xml; charset=utf-8");
    }

    private static partial class HtmlTagPattern
    {
        [GeneratedRegex("<[^>]+>")]
        internal static partial Regex Instance();
    }

    private static string StripHtmlTags(string html) =>
        HtmlTagPattern.Instance().Replace(html, "").Trim();
}

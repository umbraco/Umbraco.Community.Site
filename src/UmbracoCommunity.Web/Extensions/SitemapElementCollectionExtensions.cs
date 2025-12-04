using System.Text;
using System.Xml.Linq;
using UmbracoCommunity.Web.Models.ServiceModels;

namespace UmbracoCommunity.Web.Extensions;

internal static class SitemapElementCollectionExtensions
{
    /// <summary>
    /// Creates and XML document from the provided collection of <see cref="SitemapElement" />.
    /// </summary>
    /// <remarks>
    /// See https://www.sitemaps.org/protocol.html for format details.
    /// </remarks>
    public static XDocument AsXml(this IReadOnlyList<SitemapElement> siteMapElements)
    {
        XNamespace xmlns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        return new(
            new XDeclaration("1.0", Encoding.UTF8.WebName, "yes"),
            new XElement(xmlns + "urlset", siteMapElements.Select(x => CreateItemElement(x, xmlns))));

    }

    private static XElement CreateItemElement(SitemapElement siteMapElement, XNamespace xmlns)
    {
        var itemElement = new XElement(xmlns + "url", new XElement(xmlns + "loc", siteMapElement.Url.ToString().ToLowerInvariant()));
        itemElement.Add(new XElement(xmlns + "lastmod", siteMapElement.LastModified.ToString("yyyy-MM-dd")));
        return itemElement;
    }
}

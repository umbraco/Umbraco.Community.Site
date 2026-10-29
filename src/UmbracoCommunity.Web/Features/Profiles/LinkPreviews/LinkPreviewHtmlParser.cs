using System.Net;
using System.Text.RegularExpressions;

namespace UmbracoCommunity.Web.Features.Profiles.LinkPreviews;

/// <summary>Reads the title, share image and icon from the head of an HTML page.</summary>
public static partial class LinkPreviewHtmlParser
{
    public static LinkPreview Parse(string html, Uri pageUri)
    {
        var headEnd = html.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
        var head = headEnd >= 0 ? html[..headEnd] : html;

        string? title = null;
        string? image = null;
        string? icon = null;

        foreach (Match tag in TagPattern().Matches(head))
        {
            var attributes = ReadAttributes(tag.Value);
            if (tag.Groups[1].Value.Equals("meta", StringComparison.OrdinalIgnoreCase))
            {
                var key = (attributes.GetValueOrDefault("property") ?? attributes.GetValueOrDefault("name"))?.ToLowerInvariant();
                var content = attributes.GetValueOrDefault("content");
                if (key is "og:image" or "og:image:url" or "twitter:image")
                {
                    image ??= ResolveHttps(content, pageUri);
                }
                else if (key is "og:title")
                {
                    title ??= Clean(content, 200);
                }
            }
            else if (attributes.GetValueOrDefault("rel") is { } rel
                && rel.Split(' ', StringSplitOptions.RemoveEmptyEntries).Any(r => r.Equals("icon", StringComparison.OrdinalIgnoreCase)))
            {
                icon ??= ResolveHttps(attributes.GetValueOrDefault("href"), pageUri);
            }
        }

        title ??= TitlePattern().Match(head) is { Success: true } m ? Clean(m.Groups[1].Value, 200) : null;
        icon ??= new Uri(pageUri, "/favicon.ico").ToString();

        return new LinkPreview(LinkPreviewStatus.Available, title, image, icon);
    }

    private static Dictionary<string, string> ReadAttributes(string tag)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match attribute in AttributePattern().Matches(tag))
        {
            attributes.TryAdd(attribute.Groups[1].Value, WebUtility.HtmlDecode(attribute.Groups[2].Success ? attribute.Groups[2].Value : attribute.Groups[3].Value));
        }

        return attributes;
    }

    private static string? ResolveHttps(string? value, Uri pageUri)
    {
        if (string.IsNullOrWhiteSpace(value) || !Uri.TryCreate(pageUri, value.Trim(), out var resolved))
        {
            return null;
        }

        return resolved.Scheme == Uri.UriSchemeHttps && resolved.ToString().Length <= ProfileLinkUrl.MaxLength ? resolved.ToString() : null;
    }

    private static string? Clean(string? value, int maxLength)
    {
        var text = WebUtility.HtmlDecode(value ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return null;
        }

        return text.Length <= maxLength ? text : text[..maxLength];
    }

    [GeneratedRegex(@"<(meta|link)\b[^>]*>", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex TagPattern();

    [GeneratedRegex("([a-zA-Z:_-]+)\\s*=\\s*(?:\"([^\"]*)\"|'([^']*)')", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex AttributePattern();

    [GeneratedRegex(@"<title[^>]*>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline, matchTimeoutMilliseconds: 1000)]
    private static partial Regex TitlePattern();
}

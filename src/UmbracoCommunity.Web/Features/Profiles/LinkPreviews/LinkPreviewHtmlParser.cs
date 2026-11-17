using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace UmbracoCommunity.Web.Features.Profiles.LinkPreviews;

/// <summary>
/// Reads the title, description, share image, icon, type and (best effort) published date from an HTML page: the
/// <c>meta</c> and <c>link</c> tags of its head, then for the date a JSON-LD <c>datePublished</c> or the first
/// <c>&lt;time datetime&gt;</c> anywhere in what was read.
/// </summary>
public static partial class LinkPreviewHtmlParser
{
    private const int MaxDescriptionLength = 300;

    public static LinkPreview Parse(string html, Uri pageUri)
    {
        var headEnd = html.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
        var head = headEnd >= 0 ? html[..headEnd] : html;

        string? title = null;
        string? image = null;
        string? icon = null;
        string? description = null;
        string? metaDescription = null;
        string? twitterTitle = null;
        string? pageType = null;
        DateOnly? published = null;

        foreach (Match tag in TagPattern().Matches(head))
        {
            var attributes = ReadAttributes(tag.Value);
            if (tag.Groups[1].Value.Equals("meta", StringComparison.OrdinalIgnoreCase))
            {
                var key = (attributes.GetValueOrDefault("property")
                    ?? attributes.GetValueOrDefault("name")
                    ?? attributes.GetValueOrDefault("itemprop"))?.ToLowerInvariant();
                var content = attributes.GetValueOrDefault("content");
                if (key is "og:image" or "og:image:url" or "twitter:image")
                {
                    image ??= ResolveHttps(content, pageUri);
                }
                else if (key is "og:title")
                {
                    title ??= Clean(content, 200);
                }
                else if (key is "twitter:title")
                {
                    twitterTitle ??= Clean(content, 200);
                }
                else if (key is "og:type")
                {
                    pageType ??= Clean(content, 50)?.ToLowerInvariant();
                }
                else if (key is "article:published_time" or "og:published_time" or "og:article:published_time" or "datepublished")
                {
                    published ??= ParseDate(content);
                }
                else if (key is "og:description" or "twitter:description")
                {
                    description ??= Clean(content, MaxDescriptionLength);
                }
                else if (key is "description")
                {
                    metaDescription ??= Clean(content, MaxDescriptionLength);
                }
            }
            else if (attributes.GetValueOrDefault("rel") is { } rel
                && rel.Split(' ', StringSplitOptions.RemoveEmptyEntries).Any(r => r.Equals("icon", StringComparison.OrdinalIgnoreCase)))
            {
                icon ??= ResolveHttps(attributes.GetValueOrDefault("href"), pageUri);
            }
        }

        title ??= twitterTitle;
        title ??= TitlePattern().Match(head) is { Success: true } m ? Clean(m.Groups[1].Value, 200) : null;
        icon ??= new Uri(pageUri, "/favicon.ico").ToString();
        published ??= JsonLdDatePattern().Match(html) is { Success: true } jsonLd ? ParseDate(jsonLd.Groups[1].Value) : null;
        published ??= TimePattern().Match(html) is { Success: true } time ? ParseDate(time.Groups[1].Success ? time.Groups[1].Value : time.Groups[2].Value) : null;

        return new LinkPreview(LinkPreviewStatus.Available, title, image, icon, description ?? metaDescription, published, pageType);
    }

    /// <summary>The day a date or date-time says, as written (not moved to UTC), or null when it isn't one.</summary>
    private static DateOnly? ParseDate(string? value)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text) || text.Length > 64)
        {
            return null;
        }

        return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date)
            ? DateOnly.FromDateTime(date.DateTime)
            : null;
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

    [GeneratedRegex("\"datePublished\"\\s*:\\s*\"([^\"]{1,64})\"", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex JsonLdDatePattern();

    [GeneratedRegex("<time\\b[^>]*\\bdatetime\\s*=\\s*(?:\"([^\"]{1,64})\"|'([^']{1,64})')", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex TimePattern();

    [GeneratedRegex(@"<title[^>]*>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline, matchTimeoutMilliseconds: 1000)]
    private static partial Regex TitlePattern();
}

using System.Text.RegularExpressions;
using UmbracoCommunity.Web.Models.PublishedModels;

namespace UmbracoCommunity.Web.Extensions;

public static class BlockIntroExtensions
{
    public const string IntroBesideClass = "intro-beside";

    private static readonly Regex LeadingHeading = new(
        @"^\s*(<h([1-6])\b[^>]*>.*?</h\2>)",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled,
        TimeSpan.FromSeconds(1));

    public static bool HasIntro(this IContentBlockIntro? content)
        => content != null && (!string.IsNullOrEmpty(content.Title) || !content.Subtitle.IsEmptyHtmlString());

    public static string? IntroLayoutClass(this IContentBlockIntro? content, ISettingsIntroLayout? settings)
        => settings?.IntroBesideContent == true && content.HasIntro() ? IntroBesideClass : null;

    public static (string Heading, string Body)? SplitLeadingHeading(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return null;
        }

        var match = LeadingHeading.Match(html);
        if (!match.Success)
        {
            return null;
        }

        var body = html[match.Length..];
        return string.IsNullOrWhiteSpace(body) ? null : (match.Groups[1].Value, body);
    }
}

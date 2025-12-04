using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Web;
using Umbraco.Cms.Core.Strings;

namespace UmbracoCommunity.Web.Extensions;

public static class HtmlHelperExtensions
{
    private const string ScriptsKey = "DelayedScripts";

    public static IDisposable BeginScripts(this IHtmlHelper helper)
        => new ScriptBlock(helper.ViewContext);

    public static HtmlString PageScripts(this IHtmlHelper helper)
        => new(string.Join(Environment.NewLine, GetPageItemsList(helper.ViewContext.HttpContext, ScriptsKey)));

    private static List<string> GetPageItemsList(HttpContext httpContext, string key)
    {
        if (httpContext.Items[key] is not List<string> pageItems)
        {
            pageItems = [];
            httpContext.Items[key] = pageItems;
        }

        return pageItems;
    }

    private class ScriptBlock : IDisposable
    {
        private readonly TextWriter _originalWriter;
        private readonly StringWriter _scriptsWriter;

        private readonly ViewContext _viewContext;

        public ScriptBlock(ViewContext viewContext)
        {
            _viewContext = viewContext;
            _originalWriter = _viewContext.Writer;
            _viewContext.Writer = _scriptsWriter = new StringWriter();
        }

        public void Dispose()
        {
            _viewContext.Writer = _originalWriter;

            List<string> pageScripts = GetPageItemsList(_viewContext.HttpContext, ScriptsKey);

            pageScripts.Add(_scriptsWriter.ToString());
        }
    }

    public static IHtmlEncodedString ReplaceLineBreaks(string text)
    {
        var result = HttpUtility.HtmlEncode(text)
            ?.Replace("\r\n", "<br />")
            .Replace("\r", "<br />")
            .Replace("\n", "<br />");

        return new HtmlEncodedString(result ?? string.Empty);
    }

    public static IHtmlContent RenderButtonCTA(
       this IHtmlHelper htmlHelper,
       string? href,
       string? text,
       string? target = null,
       string? title = null,
       ButtonLinkTheme theme = ButtonLinkTheme.Blue)
    {
        if (string.IsNullOrEmpty(href) || string.IsNullOrEmpty(text))
        {
            return HtmlString.Empty;
        }
        return htmlHelper.RenderButtonLinkWithArrow(href, text, "cta", target, title, theme);
    }

    public static IHtmlContent RenderButtonLinkWithArrow(
       this IHtmlHelper htmlHelper,
       string? href,
       string? text,
       string? @class = null,
       string? target = null,
       string? title = null,
       ButtonLinkTheme theme = ButtonLinkTheme.Blue)
    {
        if (string.IsNullOrEmpty(href) || string.IsNullOrEmpty(text))
        {
            return HtmlString.Empty;
        }
        var textWithArrow = text + "<svg width=\"22\" height=\"22\" viewBox=\"0 0 22 22\" fill=\"none\" xmlns=\"http://www.w3.org/2000/svg\"><g><path id=\"Vector\" d=\"M6.41675 6.41663H15.5834V15.5833\" stroke=\"#283A97\" stroke-width=\"1.5\" stroke-linecap=\"round\" stroke-linejoin=\"round\" /><path id=\"Vector_2\" d=\"M6.41675 15.5833L15.5834 6.41663\" stroke=\"#283A97\" stroke-width=\"1.5\" stroke-linecap=\"round\" stroke-linejoin=\"round\" /></g></svg>";

        return htmlHelper.RenderButtonLink(href, textWithArrow, $"arrow {@class}", target, title, theme);
    }

    public static IHtmlContent RenderButtonLink(
        this IHtmlHelper htmlHelper,
        string? href,
        string? text,
        string? @class = null,
        string? target = null,
        string? title = null,
        ButtonLinkTheme theme = ButtonLinkTheme.Default)
    {
        if (string.IsNullOrEmpty(href) || string.IsNullOrEmpty(text))
        {
            return HtmlString.Empty;
        }
        Dictionary<string, string> attributes = [];

        string themeClass = theme != ButtonLinkTheme.Default ? $"is-{theme.ToString().ToLowerInvariant()}" : "";

        attributes.Add("href", href);
        attributes.Add("class", $"btn {@class} {themeClass}");

        if (!string.IsNullOrEmpty(target))
        {
            attributes.Add("target", target);
        }

        if (!string.IsNullOrEmpty(title))
        {
            attributes.Add("title", title ?? text);
        }

        return htmlHelper.Raw($"<a {string.Join(" ", attributes.Select(n => $"{n.Key}=\"{n.Value}\""))}>{text}</a>");
    }

    public enum ButtonLinkTheme
    {
        Default, Blue, WhitePink, White, Transparent, TransparentBlue
    }
}

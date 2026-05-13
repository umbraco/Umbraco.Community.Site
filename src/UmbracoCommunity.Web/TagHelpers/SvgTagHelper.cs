using HtmlAgilityPack;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Logging;
using StackExchange.Profiling.Internal;
using System.Text.RegularExpressions;
using Umbraco.Cms.Core.IO;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Routing;

namespace UmbracoCommunity.Web.TagHelpers
{
    [HtmlTargetElement("svg-src")]
    public class SvgTagHelper : TagHelper
    {
        private readonly IPublishedUrlProvider _urlProvider;
        private readonly MediaFileManager _mediaFileManager;
        private readonly ILogger<SvgTagHelper> _logger;

        public SvgTagHelper(MediaFileManager mediaFileManager, IPublishedUrlProvider urlProvider, ILogger<SvgTagHelper> logger)
        {
            _mediaFileManager = mediaFileManager;
            _urlProvider = urlProvider;
            _logger = logger;
        }

        [HtmlAttributeName("media")]
        public MediaWithCrops? Media { get; set; }

        [HtmlAttributeName("width")]
        public int? Width { get; set; }

        [HtmlAttributeName("height")]
        public int? Height { get; set; }

        [HtmlAttributeName("alt-text")]
        public string? AltText { get; set; }

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            if (Media == null)
            {
                output.SuppressOutput();
                return;
            }

            // ensure .svg file extension
            var mediaItemPath = Media.Url(_urlProvider);
            if (mediaItemPath?.EndsWith(".svg", StringComparison.InvariantCultureIgnoreCase) != true)
            {
                output.SuppressOutput();
                return;
            }

            // ensure exists
            if (_mediaFileManager.FileSystem.FileExists(mediaItemPath) == false)
            {
                output.SuppressOutput();
                return;
            }

            var fileStream = _mediaFileManager.FileSystem.OpenFile(mediaItemPath);
            using var reader = new StreamReader(fileStream);
            var fileContents = reader.ReadToEnd();

            var cleanedFileContents = Regex.Replace(fileContents,
                @"<script.*?script>",
                @"",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            cleanedFileContents = Regex.Replace(cleanedFileContents,
                @"javascript:",
                @"syntax:error:",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            if (string.IsNullOrEmpty(cleanedFileContents))
            {
                output.SuppressOutput();
                return;
            }

            output.Attributes.RemoveAll("media");

            HtmlDocument doc = new HtmlDocument();
            try
            {
                doc.LoadHtml(cleanedFileContents);
                var svgs = doc.DocumentNode.SelectNodes("//svg");
                if (svgs != null && svgs.Count > 0)
                {
                    foreach (var svgNode in svgs)
                    {
                        if (Width.HasValue)
                        {
                            svgNode.SetAttributeValue("width", Width.Value.ToString());
                        }
                        if (Height.HasValue)
                        {
                            svgNode.SetAttributeValue("height", Height.Value.ToString());
                        }
                        if (AltText.HasValue())
                        {
                            svgNode.SetAttributeValue("alt", AltText.ToString());
                        }

                        ScopeInlineStyles(svgNode);
                    }

                    cleanedFileContents = doc.DocumentNode.OuterHtml;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing svg for html output");
            }

            output.TagName = null;
            output.Content.SetHtmlContent(cleanedFileContents);
        }

        // Adds a unique class to <svg> and prefixes every selector in its inline
        // <style> blocks with that class. Illustrator-exported class names like
        // .st0–.stN are document-scoped by default, so without this they bleed
        // across every SVG on the page that happens to share class names.
        private static void ScopeInlineStyles(HtmlNode svgNode)
        {
            var styleNodes = svgNode.SelectNodes(".//style");
            if (styleNodes == null || styleNodes.Count == 0)
            {
                return;
            }

            var scopeClass = $"svg-{Guid.NewGuid():N}".Substring(0, 12);
            var existing = svgNode.GetAttributeValue("class", string.Empty);
            svgNode.SetAttributeValue(
                "class",
                string.IsNullOrEmpty(existing) ? scopeClass : $"{existing} {scopeClass}");

            foreach (var styleNode in styleNodes)
            {
                styleNode.InnerHtml = PrefixCssSelectors(styleNode.InnerHtml, scopeClass);
            }
        }

        private static readonly Regex SelectorRegex = new(
            @"(^|\})\s*(?<sel>[^@{}][^{}]*?)\s*\{",
            RegexOptions.Compiled);

        private static string PrefixCssSelectors(string css, string scopeClass) =>
            SelectorRegex.Replace(css, m =>
            {
                var prefixed = string.Join(
                    ", ",
                    m.Groups["sel"].Value.Split(',').Select(s => $".{scopeClass} {s.Trim()}"));
                return $"{m.Groups[1].Value}{prefixed} {{";
            });
    }
}
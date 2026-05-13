// Originally adapted from InlineSvgTagHelper in the Our.Umbraco.TagHelpers
// community project (MIT licensed): https://github.com/umbraco-community/Our-Umbraco-TagHelpers
// Created by Warren Buckley, with later contributions from AndyBoot, drpeck,
// and profcinders. This version is reshaped for the Umbraco Community site —
// media-only source, security sanitisation, and per-SVG <style> scoping — but
// the inlining approach and the use of HtmlAgilityPack are theirs.

using HtmlAgilityPack;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Logging;
using StackExchange.Profiling.Internal;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Umbraco.Cms.Core.Cache;
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
        private readonly AppCaches _appCaches;
        private readonly ILogger<SvgTagHelper> _logger;

        // How long the post-scope SVG markup stays in the runtime cache after
        // a miss. The scoped output is deterministic per media path, so every
        // render of the same SVG shares the same cached blob.
        private static readonly TimeSpan SvgCacheTtl = TimeSpan.FromMinutes(60);

        public SvgTagHelper(
            MediaFileManager mediaFileManager,
            IPublishedUrlProvider urlProvider,
            AppCaches appCaches,
            ILogger<SvgTagHelper> logger)
        {
            _mediaFileManager = mediaFileManager;
            _urlProvider = urlProvider;
            _appCaches = appCaches;
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

            var scopedSvg = _appCaches.RuntimeCache.GetCacheItem<string?>(
                $"svg-scoped::{mediaItemPath}",
                () => ReadSanitiseAndScope(mediaItemPath),
                timeout: SvgCacheTtl);

            if (string.IsNullOrEmpty(scopedSvg))
            {
                output.SuppressOutput();
                return;
            }

            output.Attributes.RemoveAll("media");

            // If no per-call attributes need injecting, emit the cached output
            // as-is and skip the parse.
            if (!Width.HasValue && !Height.HasValue && !AltText.HasValue())
            {
                output.TagName = null;
                output.Content.SetHtmlContent(scopedSvg);
                return;
            }

            // Otherwise re-parse to inject the per-call attributes onto the SVG root.
            HtmlDocument doc = new HtmlDocument();
            try
            {
                doc.LoadHtml(scopedSvg);
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
                    }
                    scopedSvg = doc.DocumentNode.OuterHtml;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error injecting attributes into svg output");
            }

            output.TagName = null;
            output.Content.SetHtmlContent(scopedSvg);
        }

        // Reads the SVG from media storage, sanitises any script/javascript:
        // payload, then scopes inline <style> selectors so class names like
        // .st0 don't bleed across SVGs that share them. Returns the post-scope
        // markup, or null if the file can't be read or was empty.
        private string? ReadSanitiseAndScope(string mediaItemPath)
        {
            if (_mediaFileManager.FileSystem.FileExists(mediaItemPath) == false)
            {
                return null;
            }

            var fileStream = _mediaFileManager.FileSystem.OpenFile(mediaItemPath);
            using var reader = new StreamReader(fileStream);
            var contents = reader.ReadToEnd();

            contents = Regex.Replace(contents,
                @"<script.*?script>",
                string.Empty,
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            contents = Regex.Replace(contents,
                @"javascript:",
                @"syntax:error:",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            if (string.IsNullOrEmpty(contents))
            {
                return null;
            }

            var doc = new HtmlDocument();
            try
            {
                doc.LoadHtml(contents);
                var svgs = doc.DocumentNode.SelectNodes("//svg");
                if (svgs != null && svgs.Count > 0)
                {
                    var scopeClass = GetScopeClass(mediaItemPath);
                    foreach (var svgNode in svgs)
                    {
                        ScopeInlineStyles(svgNode, scopeClass);
                    }
                    return doc.DocumentNode.OuterHtml;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing svg for html output");
                return null;
            }

            return contents;
        }

        // Derive a stable scope class from the media path so every render of
        // the same SVG produces identical scoped markup (which is what makes
        // the cache useful). Two instances of the same SVG on one page sharing
        // a scope class is harmless — their <style> rules are identical, so
        // there's nothing to bleed.
        private static string GetScopeClass(string mediaPath)
        {
            var bytes = SHA1.HashData(Encoding.UTF8.GetBytes(mediaPath));
            return "svg-" + Convert.ToHexString(bytes).Substring(0, 12).ToLowerInvariant();
        }

        // Adds the scope class to the <svg> element and prefixes every
        // selector in its inline <style> blocks with that class.
        private static void ScopeInlineStyles(HtmlNode svgNode, string scopeClass)
        {
            var styleNodes = svgNode.SelectNodes(".//style");
            if (styleNodes == null || styleNodes.Count == 0) return;

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
using Microsoft.AspNetCore.Razor.TagHelpers;
using System.Text.RegularExpressions;
using Umbraco.Cms.Core.IO;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Routing;

namespace UmbracoCommunity.Web.TagHelpers
{
    [HtmlTargetElement("svg-src")]
    public class SvgTagHelper : TagHelper
    {
        private IPublishedUrlProvider _urlProvider;
        private MediaFileManager _mediaFileManager;

        public SvgTagHelper(MediaFileManager mediaFileManager, IPublishedUrlProvider urlProvider)
        {
            _mediaFileManager = mediaFileManager;
            _urlProvider = urlProvider;
        }

        [HtmlAttributeName("media")]
        public MediaWithCrops? Media { get; set; }

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

            output.TagName = null;
            output.Content.SetHtmlContent(cleanedFileContents);
        }
    }
}
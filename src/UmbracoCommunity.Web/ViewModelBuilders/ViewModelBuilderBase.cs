using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.Web;
using Umbraco.Cms.Core.Media;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Blocks;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Routing;
using UmbracoCommunity.Web.Models.PublishedModels;
using UmbracoCommunity.Web.Models.ViewModels.Blocks;

namespace UmbracoCommunity.Web.ViewModelBuilders
{
    public abstract class ViewModelBuilderBase
    {
        protected static IList<BlockGridRow> ParseBlockGrid(BlockGridModel? contentBlocks)
        {
            var rows = new List<BlockGridRow>();
            if (contentBlocks != null && contentBlocks.Any())
            {
                var columnSpan = contentBlocks.GridColumns ?? 12;

                var columnCount = 0;
                var blockRow = new BlockGridRow();
                foreach (var block in contentBlocks)
                {
                    var newSpan = block.ColumnSpan + columnCount;

                    if (newSpan == columnSpan || contentBlocks.Last() == block)
                    {
                        blockRow.Blocks.Add(block);
                        blockRow.HasMultipleBlocks = blockRow.Blocks.Count > 1;
                        rows.Add(blockRow);
                        blockRow = new BlockGridRow();

                        columnCount = 0;
                    }
                    else if (newSpan > columnSpan)
                    {
                        // add row to content
                        if (blockRow.Blocks.Any())
                        {
                            blockRow.HasMultipleBlocks = blockRow.Blocks.Count > 1;
                            rows.Add(blockRow);
                        }

                        // create new row
                        blockRow = new BlockGridRow
                        {
                            Blocks = [block]
                        };

                        // set columnCount
                        columnCount = block.ColumnSpan;
                    }
                    else
                    {
                        blockRow.Blocks.Add(block);
                        // update column count
                        columnCount += block.ColumnSpan;
                    }
                }
            }
            foreach (var row in rows)
            {
                var firstBlock = row.Blocks.FirstOrDefault();
                if (firstBlock != null && firstBlock.Settings != null && firstBlock.Settings is ISettingsColour colourSettings)
                {
                    if (colourSettings.BackgroundColour != null)
                    {
                        row.BackgroundColour = colourSettings.BackgroundColour;//colourSettings.BackgroundColour.Color;
                        if (!string.Equals(row.BackgroundColour, "#ffffff", StringComparison.InvariantCultureIgnoreCase))
                        {
                            row.HasBg = true;
                        }
                    }
                }
            }
            return rows;
        }

        public static string? GetWebPageName(ISeo contentModel)
        {
            if (contentModel is Home)
            {
                return contentModel.MetaTitle;
            }

            if (contentModel is Blog && !string.IsNullOrEmpty(contentModel.MetaTitle))
            {
                return contentModel.MetaTitle;
            }

            return (contentModel as IPublishedContent)?.Name;
        }

        private static readonly string[] s_noWebpConversionTypes = ["webp"];

        protected static string? GetImageUrl(
            MediaWithCrops? mediaWithCrops,
            string cropAlias,
            IImageUrlGenerator imageUrlGenerator,
            IPublishedValueFallback publishedValueFallback,
            IPublishedUrlProvider publishedUrlProvider,
            UrlMode mode = UrlMode.Relative,
            bool? localCropsOnly = false,
            int? height = null,
            int? width = null,
            bool? webp = true)
        {
            if (mediaWithCrops is null)
            {
                return null;
            }

            string? cropUrl;

            if (width is not null)
            {
                cropUrl = mediaWithCrops.GetCropUrl(
                    imageUrlGenerator,
                    publishedValueFallback,
                    publishedUrlProvider,
                    width: width,
                    urlMode: mode,
                    imageCropMode: ImageCropMode.Pad);
            }
            else if (height is not null)
            {
                cropUrl = mediaWithCrops.GetCropUrl(
                    imageUrlGenerator,
                    publishedValueFallback,
                    publishedUrlProvider,
                    height: height,
                    urlMode: mode,
                    imageCropMode: ImageCropMode.Pad);
            }
            else if (localCropsOnly is false || mediaWithCrops.LocalCrops.HasCrops() is false)
            {
                cropUrl = mediaWithCrops.GetCropUrl(
                    cropAlias,
                    imageUrlGenerator,
                    publishedValueFallback,
                    publishedUrlProvider,
                    mode);
            }
            else
            {
                cropUrl = mediaWithCrops.LocalCrops.GetCropUrl(cropAlias, imageUrlGenerator);
            }

            return CanConvertToWebp(cropUrl, mediaWithCrops, webp) ? AppendWebpFormatter(cropUrl) : cropUrl;
        }

        private static bool CanConvertToWebp([NotNullWhen(true)] string? cropUrl, MediaWithCrops mediaWithCrops, bool? webp = true) =>
        cropUrl is not null &&
        (webp ?? true) &&
        mediaWithCrops.Content is not UmbracoMediaVectorGraphics &&
        s_noWebpConversionTypes.Contains((mediaWithCrops.Content as Image)?.UmbracoExtension) == false;

        private static string AppendWebpFormatter(string cropUrl)
        {
            // cropUrl is usually, but not always, relative, UriBuilder requires absolute.
            bool isRelative = cropUrl.StartsWith('/');
            UriBuilder cropUrlBuilder = new(isRelative ? $"http://example.com{cropUrl}" : cropUrl);
            NameValueCollection query = HttpUtility.ParseQueryString(cropUrlBuilder.Query);
            query["format"] = "webp";
            cropUrlBuilder.Query = query.ToString();

            return isRelative ? cropUrlBuilder.Path + cropUrlBuilder.Query : cropUrlBuilder.Uri.ToString();
        }
    }
}

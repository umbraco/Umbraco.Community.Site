using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.Web;
using Umbraco.Cms.Core.Media;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Routing;
using UmbracoCommunity.Web.Models.PublishedModels;

namespace UmbracoCommunity.Web.Helpers
{
    public static class ImageHelper
    {
        private static readonly string[] s_noWebpConversionTypes = ["webp"];
        private static readonly string[] s_vectorGraphicTypes = ["svg"];

        public static string? GetImageUrl(
            this MediaWithCrops? mediaWithCrops,
            string cropAlias,
            IImageUrlGenerator imageUrlGenerator,
            IPublishedValueFallback publishedValueFallback,
            IPublishedUrlProvider publishedUrlProvider,
            UrlMode mode = UrlMode.Relative,
            bool? localCropsOnly = false,
            int? height = null,
            int? width = null,
            bool? webp = true,
            int? quality = 85)
        {
            if (mediaWithCrops is null)
            {
                return null;
            }

            if (IsVectorImage(mediaWithCrops))
            {
                return publishedUrlProvider.GetMediaUrl(mediaWithCrops);
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
                    imageCropMode: ImageCropMode.Pad,
                    quality: quality);
            }
            else if (height is not null)
            {
                cropUrl = mediaWithCrops.GetCropUrl(
                    imageUrlGenerator,
                    publishedValueFallback,
                    publishedUrlProvider,
                    height: height,
                    urlMode: mode,
                    imageCropMode: ImageCropMode.Pad,
                    quality: quality);
            }
            else if (localCropsOnly is false || mediaWithCrops.LocalCrops.HasCrops() is false)
            {
                cropUrl = mediaWithCrops.GetCropUrl(
                    imageUrlGenerator,
                    publishedValueFallback,
                    publishedUrlProvider,
                    cropAlias: cropAlias,
                    quality: quality,
                    urlMode: mode,
                    useCropDimensions: true);
            }
            else
            {
                var localCropUrl = mediaWithCrops.LocalCrops.GetCropUrl(cropAlias, imageUrlGenerator);
                cropUrl = localCropUrl is not null ? $"{localCropUrl}&quality={quality}" : null;
            }

            return CanConvertToWebp(cropUrl, mediaWithCrops) ? PrependWebpFormatter(cropUrl) : cropUrl;
        }

        private static bool IsVectorImage(MediaWithCrops mediaWithCrops) =>
            mediaWithCrops.Content is UmbracoMediaVectorGraphics ||
            s_vectorGraphicTypes.Contains((mediaWithCrops.Content as Image)?.UmbracoExtension);

        public static bool CanConvertToWebp([NotNullWhen(true)] string? cropUrl, MediaWithCrops mediaWithCrops) =>
        cropUrl is not null &&
        mediaWithCrops.Content is not UmbracoMediaVectorGraphics &&
        s_noWebpConversionTypes.Contains((mediaWithCrops.Content as Image)?.UmbracoExtension) == false;

        public static string? PrependWebpFormatter(this string? cropUrl)
        {
            if (string.IsNullOrWhiteSpace(cropUrl)) return cropUrl;
            // cropUrl is usually, but not always, relative, UriBuilder requires absolute.
            bool isRelative = cropUrl.StartsWith('/');
            UriBuilder cropUrlBuilder = new(isRelative ? $"http://example.com{cropUrl}" : cropUrl);
            NameValueCollection existingQuery = HttpUtility.ParseQueryString(cropUrlBuilder.Query);

            // Build query string with format=webp first, then existing parameters
            var queryParts = new List<string> { "format=webp" };
            foreach (string? key in existingQuery.AllKeys)
            {
                if (key is not null && !key.Equals("format", StringComparison.OrdinalIgnoreCase))
                {
                    queryParts.Add($"{HttpUtility.UrlEncode(key)}={HttpUtility.UrlEncode(existingQuery[key])}");
                }
            }
            cropUrlBuilder.Query = string.Join("&", queryParts);

            return isRelative ? cropUrlBuilder.Path + cropUrlBuilder.Query : cropUrlBuilder.Uri.ToString();
        }
    }
}

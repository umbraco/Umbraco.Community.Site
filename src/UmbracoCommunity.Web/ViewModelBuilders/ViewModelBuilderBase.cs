using Umbraco.Cms.Core.Media;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Routing;
using UmbracoCommunity.Web.Models.PublishedModels;

namespace UmbracoCommunity.Web.ViewModelBuilders
{
    public abstract class ViewModelBuilderBase
    {
        public static string? GetWebPageName(ICompositionSeo contentModel)
        {
            if (contentModel is Home or EventsHome)
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
        private static readonly string[] s_vectorGraphicTypes = ["svg"];

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

            // format=webp has to be passed through GetCropUrl's furtherOptions so it's part
            // of the HMAC-signed URL. Appending it afterwards would invalidate the HMAC and
            // ImageSharp.Web would return 400.
            var furtherOptions = ShouldConvertToWebp(mediaWithCrops, webp) ? "format=webp" : null;

            if (width is not null)
            {
                return mediaWithCrops.GetCropUrl(
                    imageUrlGenerator,
                    publishedValueFallback,
                    publishedUrlProvider,
                    width: width,
                    urlMode: mode,
                    imageCropMode: ImageCropMode.Pad,
                    quality: quality,
                    furtherOptions: furtherOptions);
            }
            if (height is not null)
            {
                return mediaWithCrops.GetCropUrl(
                    imageUrlGenerator,
                    publishedValueFallback,
                    publishedUrlProvider,
                    height: height,
                    urlMode: mode,
                    imageCropMode: ImageCropMode.Pad,
                    quality: quality,
                    furtherOptions: furtherOptions);
            }
            if (localCropsOnly is false || mediaWithCrops.LocalCrops.HasCrops() is false)
            {
                return mediaWithCrops.GetCropUrl(
                    imageUrlGenerator,
                    publishedValueFallback,
                    publishedUrlProvider,
                    cropAlias: cropAlias,
                    quality: quality,
                    urlMode: mode,
                    useCropDimensions: true,
                    furtherOptions: furtherOptions);
            }

            // Local-crops branch: LocalCrops.GetCropUrl doesn't accept furtherOptions, and the
            // existing `&quality=...` append already invalidates any HMAC. WebP conversion is
            // not applied here for the same reason — to safely add it we'd need to re-sign.
            var localCropUrl = mediaWithCrops.LocalCrops.GetCropUrl(cropAlias, imageUrlGenerator);
            return localCropUrl is not null ? $"{localCropUrl}&quality={quality}" : null;
        }

        private static bool IsVectorImage(MediaWithCrops mediaWithCrops) =>
            mediaWithCrops.Content is UmbracoMediaVectorGraphics ||
            s_vectorGraphicTypes.Contains((mediaWithCrops.Content as Image)?.UmbracoExtension);

        private static bool ShouldConvertToWebp(MediaWithCrops mediaWithCrops, bool? webp) =>
            (webp ?? true) &&
            mediaWithCrops.Content is not UmbracoMediaVectorGraphics &&
            s_noWebpConversionTypes.Contains((mediaWithCrops.Content as Image)?.UmbracoExtension) == false;
    }
}

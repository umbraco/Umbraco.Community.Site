using Umbraco.Cms.Core.Media;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Routing;
using UmbracoCommunity.Web.Models.PublishedModels;

namespace UmbracoCommunity.Web.Services;

internal class ImageUrlBuilder : IImageUrlBuilder
{
    private static readonly string[] s_noWebpConversionTypes = ["webp"];
    private static readonly string[] s_vectorGraphicTypes = ["svg"];

    private readonly IImageUrlGenerator _imageUrlGenerator;
    private readonly IPublishedValueFallback _publishedValueFallback;
    private readonly IPublishedUrlProvider _publishedUrlProvider;

    public ImageUrlBuilder(
        IImageUrlGenerator imageUrlGenerator,
        IPublishedValueFallback publishedValueFallback,
        IPublishedUrlProvider publishedUrlProvider)
    {
        _imageUrlGenerator = imageUrlGenerator;
        _publishedValueFallback = publishedValueFallback;
        _publishedUrlProvider = publishedUrlProvider;
    }

    public string? GetImageUrl(
        MediaWithCrops? media,
        string cropAlias,
        UrlMode mode = UrlMode.Relative,
        bool? localCropsOnly = false,
        int? height = null,
        int? width = null,
        bool? webp = true,
        int? quality = 85)
    {
        if (media is null)
        {
            return null;
        }

        if (IsVectorImage(media))
        {
            return _publishedUrlProvider.GetMediaUrl(media);
        }

        // format=webp has to be passed through GetCropUrl's furtherOptions so it's part
        // of the HMAC-signed URL. Appending it afterwards would invalidate the HMAC and
        // ImageSharp.Web would return 400.
        var furtherOptions = ShouldConvertToWebp(media, webp) ? "format=webp" : null;

        if (width is not null)
        {
            return media.GetCropUrl(
                _imageUrlGenerator,
                _publishedValueFallback,
                _publishedUrlProvider,
                width: width,
                urlMode: mode,
                imageCropMode: ImageCropMode.Pad,
                quality: quality,
                furtherOptions: furtherOptions);
        }
        if (height is not null)
        {
            return media.GetCropUrl(
                _imageUrlGenerator,
                _publishedValueFallback,
                _publishedUrlProvider,
                height: height,
                urlMode: mode,
                imageCropMode: ImageCropMode.Pad,
                quality: quality,
                furtherOptions: furtherOptions);
        }
        if (localCropsOnly is false || media.LocalCrops.HasCrops() is false)
        {
            return media.GetCropUrl(
                _imageUrlGenerator,
                _publishedValueFallback,
                _publishedUrlProvider,
                cropAlias: cropAlias,
                quality: quality,
                urlMode: mode,
                useCropDimensions: true,
                furtherOptions: furtherOptions);
        }

        // Local-crops branch: LocalCrops.GetCropUrl doesn't accept furtherOptions, and the
        // existing `&quality=...` append already invalidates any HMAC. WebP conversion is
        // not applied here for the same reason — to safely add it we'd need to re-sign.
        var localCropUrl = media.LocalCrops.GetCropUrl(cropAlias, _imageUrlGenerator);
        return localCropUrl is not null ? $"{localCropUrl}&quality={quality}" : null;
    }

    private static bool IsVectorImage(MediaWithCrops media) =>
        media.Content is UmbracoMediaVectorGraphics ||
        s_vectorGraphicTypes.Contains((media.Content as Image)?.UmbracoExtension);

    private static bool ShouldConvertToWebp(MediaWithCrops media, bool? webp) =>
        (webp ?? true) &&
        media.Content is not UmbracoMediaVectorGraphics &&
        s_noWebpConversionTypes.Contains((media.Content as Image)?.UmbracoExtension) == false;
}

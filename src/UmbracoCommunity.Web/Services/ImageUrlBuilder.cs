using Umbraco.Cms.Core.Media;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Routing;
using UmbracoCommunity.Web.Helpers;

namespace UmbracoCommunity.Web.Services;

internal class ImageUrlBuilder : IImageUrlBuilder
{
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
        int? quality = 85) =>
        media.GetImageUrl(
            cropAlias,
            _imageUrlGenerator,
            _publishedValueFallback,
            _publishedUrlProvider,
            mode,
            localCropsOnly,
            height,
            width,
            webp,
            quality);
}

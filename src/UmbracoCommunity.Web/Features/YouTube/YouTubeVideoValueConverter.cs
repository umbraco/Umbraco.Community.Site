using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.PublishedCache;
using UmbracoCommunity.Web.Helpers;

namespace UmbracoCommunity.Web.Features.YouTube;

public sealed class YouTubeVideoValueConverter : PropertyValueConverterBase
{
    private readonly IPublishedMediaCache _mediaCache;

    public YouTubeVideoValueConverter(IPublishedMediaCache mediaCache) => _mediaCache = mediaCache;

    public override bool IsConverter(IPublishedPropertyType propertyType)
        => propertyType.EditorAlias == YouTubeVideoPropertyEditor.EditorAlias;

    public override Type GetPropertyValueType(IPublishedPropertyType propertyType) => typeof(YouTubeVideo);

    // Elements, so a replaced poster image shows without republishing the page.
    public override PropertyCacheLevel GetPropertyCacheLevel(IPublishedPropertyType propertyType) => PropertyCacheLevel.Elements;

    public override bool? IsValue(object? value, PropertyValueLevel level)
        => level == PropertyValueLevel.Source
            ? YouTubeVideoValue.TryParse(value)?.VideoId is { Length: > 0 }
            : value is not null;

    public override object? ConvertSourceToIntermediate(IPublishedElement owner, IPublishedPropertyType propertyType, object? source, bool preview)
        => YouTubeVideoValue.TryParse(source);

    public override object? ConvertIntermediateToObject(IPublishedElement owner, IPublishedPropertyType propertyType, PropertyCacheLevel referenceCacheLevel, object? inter, bool preview)
    {
        if (inter is not YouTubeVideoValue { Url: { } url } value || VideoHelper.GetYouTubeVideoId(url) is not { } videoId)
        {
            return null;
        }

        var poster = value.PosterKey is { } posterKey ? _mediaCache.GetById(preview, posterKey) : null;
        return new YouTubeVideo(url, videoId, value.Title, poster);
    }
}

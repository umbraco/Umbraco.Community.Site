using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace UmbracoCommunity.Web.Services;

/// <summary>
/// Builds HMAC-signed image URLs from <see cref="MediaWithCrops"/> for use in views,
/// wrapping the three Umbraco services that crop-URL generation needs so callers only
/// inject one thing.
/// </summary>
public interface IImageUrlBuilder
{
    string? GetImageUrl(
        MediaWithCrops? media,
        string cropAlias,
        UrlMode mode = UrlMode.Relative,
        bool? localCropsOnly = false,
        int? height = null,
        int? width = null,
        bool? webp = true,
        int? quality = 85);
}

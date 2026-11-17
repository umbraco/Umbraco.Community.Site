using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Media;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Routing;
using Umbraco.Extensions;

namespace UmbracoCommunity.Web.Features.Profiles;

/// <summary>
/// Resolves the URL a member's avatar should render at: an uploaded custom avatar (Umbraco
/// Media) if one exists, otherwise the GitHub avatar default. Shared between
/// <see cref="MemberProfileDataProvider"/> and <c>ProfileApiController</c> so the fallback
/// logic lives in exactly one place. An uploaded avatar is served as a square crop resized on
/// the server: handing the browser the full-size upload to shrink into a small circle looks grainy.
/// </summary>
public class ProfileAvatarUrlResolver
{
    private readonly IPublishedContentQuery _contentQuery;
    private readonly IPublishedUrlProvider _publishedUrlProvider;
    private readonly IImageUrlGenerator _imageUrlGenerator;

    public ProfileAvatarUrlResolver(
        IPublishedContentQuery contentQuery,
        IPublishedUrlProvider publishedUrlProvider,
        IImageUrlGenerator imageUrlGenerator)
    {
        _contentQuery = contentQuery;
        _publishedUrlProvider = publishedUrlProvider;
        _imageUrlGenerator = imageUrlGenerator;
    }

    /// <summary>Large enough for the profile page's avatar on a high-density screen.</summary>
    public const int DefaultSize = 512;

    /// <summary>For the small avatar in the site header.</summary>
    public const int SmallSize = 128;

    public string Resolve(Guid? avatarMediaKey, string gitHubHandle, int size = DefaultSize)
    {
        if (avatarMediaKey.HasValue)
        {
            var media = _contentQuery.Media(avatarMediaKey.Value);
            if (media != null)
            {
                var url = media.Url(_publishedUrlProvider);
                return _imageUrlGenerator.GetImageUrl(new ImageUrlGenerationOptions(url)
                {
                    Width = size,
                    Height = size,
                    ImageCropMode = ImageCropMode.Crop,
                }) ?? url;
            }
        }

        return $"https://github.com/{gitHubHandle}.png";
    }
}

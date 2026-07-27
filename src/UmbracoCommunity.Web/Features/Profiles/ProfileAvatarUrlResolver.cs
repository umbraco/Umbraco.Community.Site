using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Routing;
using Umbraco.Extensions;

namespace UmbracoCommunity.Web.Features.Profiles;

/// <summary>
/// Resolves the URL a member's avatar should render at: an uploaded custom avatar (Umbraco
/// Media) if one exists, otherwise the GitHub avatar default. Shared between
/// <see cref="MemberProfileDataProvider"/> and <c>ProfileApiController</c> so the fallback
/// logic lives in exactly one place.
/// </summary>
public class ProfileAvatarUrlResolver
{
    private readonly IPublishedContentQuery _contentQuery;
    private readonly IPublishedUrlProvider _publishedUrlProvider;

    public ProfileAvatarUrlResolver(IPublishedContentQuery contentQuery, IPublishedUrlProvider publishedUrlProvider)
    {
        _contentQuery = contentQuery;
        _publishedUrlProvider = publishedUrlProvider;
    }

    public string Resolve(Guid? avatarMediaKey, string gitHubHandle)
    {
        if (avatarMediaKey.HasValue)
        {
            var media = _contentQuery.Media(avatarMediaKey.Value);
            if (media != null)
            {
                return media.Url(_publishedUrlProvider);
            }
        }

        return $"https://github.com/{gitHubHandle}.png";
    }
}

using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Routing;
using Umbraco.Extensions;
using UmbracoCommunity.Web.Extensions;
using UmbracoCommunity.Web.Features.Profiles.Data;
using UmbracoCommunity.Web.Models.PublishedModels;
using UmbracoCommunity.Web.Models.ServiceModels;

namespace UmbracoCommunity.Web.Features.Profiles;

/// <summary>
/// The sitemap entries for the profiles of one tenant. There are none until the tenant's Settings node has
/// <c>showEnrichedProfileSections</c> switched on, the same switch that opens the profile sections to everyone.
/// </summary>
public sealed class ProfileSitemapService(
    MemberProfileStore store,
    IPublishedUrlProvider publishedUrlProvider)
{
    public async Task<IReadOnlyList<SitemapElement>> GetEntriesAsync(IPublishedContent root, CancellationToken cancellationToken = default)
    {
        if (root.GetSiteSettings()?.ShowEnrichedProfileSections != true)
        {
            return [];
        }

        var profilePage = root.GetSingletonPage<CommunityProfilePage>();
        if (profilePage is null)
        {
            return [];
        }

        var pageUrl = profilePage.Url(publishedUrlProvider, mode: UrlMode.Absolute);
        if (!Uri.TryCreate(pageUrl, UriKind.Absolute, out var baseUri))
        {
            return [];
        }

        var profiles = await store.ListVisibleAsync(cancellationToken);
        return [.. profiles.Select(p => new SitemapElement
        {
            Url = new Uri(baseUri, Uri.EscapeDataString(p.Handle) + "/"),
            LastModified = p.UpdatedUtc,
        })];
    }
}

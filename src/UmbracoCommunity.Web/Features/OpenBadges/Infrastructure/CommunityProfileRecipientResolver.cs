using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Web;
using UmbracoCommunity.Web.Models.PublishedModels;

namespace UmbracoCommunity.Web.Features.OpenBadges.Infrastructure;

/// <summary>
/// <see cref="IOpenBadgeRecipientResolver"/> for the tenant whose domain matches the request (falling back to the first
/// root, as <c>RobotsController</c> does for local setups without domains). The URL is
/// <c>{baseUri}{profile page path}/{handle}</c>, the same shape the account page links to.
/// </summary>
internal sealed class CommunityProfileRecipientResolver(
    IUmbracoContextFactory umbracoContextFactory,
    IPublishedContentQuery publishedContentQuery,
    IPublishedUrlProvider publishedUrlProvider) : IOpenBadgeRecipientResolver
{
    public string? GetProfileUrl(Uri baseUri, string handle)
    {
        using var reference = umbracoContextFactory.EnsureUmbracoContext();
        var umbracoContext = reference.UmbracoContext;

        var domains = umbracoContext.Domains?.GetAll(includeWildcards: false) ?? [];
        var domain = DomainUtilities.SelectDomain(domains, baseUri);
        var tenantRoot = (domain is not null ? umbracoContext.Content?.GetById(domain.ContentId) : null)
            ?? publishedContentQuery.ContentAtRoot().FirstOrDefault();

        var profilePage = tenantRoot?.DescendantsOrSelf<CommunityProfilePage>().FirstOrDefault();
        if (profilePage is null)
        {
            return null;
        }

        var path = profilePage.Url(publishedUrlProvider, mode: UrlMode.Relative);
        if (string.IsNullOrEmpty(path) || path == "#")
        {
            return null;
        }

        return new Uri(baseUri, $"{path.TrimEnd('/')}/{Uri.EscapeDataString(handle)}").ToString();
    }
}

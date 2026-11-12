using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;
using UmbracoCommunity.Web.Models.PublishedModels;

namespace UmbracoCommunity.Web.Services;

public class ContentContextService
{
    private readonly IUmbracoContextAccessor _umbracoContextAccessor;

    public ContentContextService(IUmbracoContextAccessor umbracoContextAccessor)
    {
        _umbracoContextAccessor = umbracoContextAccessor;
    }

    public IPublishedContent? CurrentPage =>
        _umbracoContextAccessor.TryGetUmbracoContext(out var context)
            ? context.PublishedRequest?.PublishedContent
            : null;

    public IUmbracoContext? UmbracoContext =>
        _umbracoContextAccessor.TryGetUmbracoContext(out var context)
            ? context
            : null;

    public string? ContentTypeAlias => CurrentPage?.ContentType.Alias;

    public string? SiteAlias => CurrentPage?.Root().ContentType.Alias;

    public bool HasFullWidthHero =>
        CurrentPage is ICompositionBannerBlock page
        && page.BannerContent?.FirstOrDefault()?.Content is HeroBannerWithVideo;

    public string Culture =>
        _umbracoContextAccessor.TryGetUmbracoContext(out var context)
            ? context.PublishedRequest?.Culture ?? Constants.Culture.Default
            : Constants.Culture.Default;
}

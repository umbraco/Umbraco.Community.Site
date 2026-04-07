using Umbraco.Cms.Core.Models.PublishedContent;

namespace UmbracoCommunity.Web.Models.Pages;

public abstract class PageViewModelBase
{
    protected PageViewModelBase(IPublishedContent currentPage)
    {
        Key = currentPage.Key;
        Name = currentPage.Name;
        ContentTypeAlias = currentPage.ContentType.Alias;
    }

    public Guid Key { get; }

    public string Name { get; }

    public string ContentTypeAlias { get; init; }

    public string Culture { get; set; } = Constants.Culture.Default;
}

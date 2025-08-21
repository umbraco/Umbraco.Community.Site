using Umbraco.Cms.Core.Models.PublishedContent;

namespace UmbracoCommunity.Web.Models.Pages;

public abstract class PageViewModelBase
{
    private readonly IList<string> _schemaMarkups = [];

    protected PageViewModelBase(IPublishedContent currentPage)
    {
        Key = currentPage.Key;
        Name = currentPage.Name;
        ContentTypeAlias = currentPage.ContentType.Alias;
    }

    public Guid Key { get; }

    public string Name { get; }

    public string ContentTypeAlias { get; }

    public string MetaTitle { get; set; } = string.Empty;

    public string MetaDescription { get; set; } = string.Empty;

    public string OpenGraphImageUrl { get; set; } = string.Empty;

    public bool HasOpenGraphImageUrl => !string.IsNullOrWhiteSpace(OpenGraphImageUrl);

    public bool HideIntercom { get; set; }

    public string Robots { get; set; } = string.Empty;

    public string? NextUrl { get; set; }

    public string? PrevUrl { get; set; }

    public string? CanonicalUrl { get; set; }

    public IReadOnlyCollection<string> SchemaMarkups => _schemaMarkups.AsReadOnly();

    public void AddSchemaMarkup(string markup) => _schemaMarkups.Add(markup);
}

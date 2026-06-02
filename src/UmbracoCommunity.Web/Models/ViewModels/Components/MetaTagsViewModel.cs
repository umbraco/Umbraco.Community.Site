namespace UmbracoCommunity.Web.Models.ViewModels.Components;

public class MetaTagsViewModel
{
    private readonly List<string> _schemaMarkups = [];

    public string Name { get; set; } = string.Empty;

    public string? SiteName { get; set; }

    public string MetaTitle { get; set; } = string.Empty;

    public string MetaDescription { get; set; } = string.Empty;

    public string OpenGraphImageUrl { get; set; } = string.Empty;

    public bool HasOpenGraphImageUrl => !string.IsNullOrWhiteSpace(OpenGraphImageUrl);

    public string Robots { get; set; } = string.Empty;

    public string? CanonicalUrl { get; set; }

    public string? NextUrl { get; set; }

    public string? PrevUrl { get; set; }

    public IReadOnlyCollection<string> SchemaMarkups => _schemaMarkups.AsReadOnly();

    public void AddSchemaMarkup(string markup) => _schemaMarkups.Add(markup);
}

namespace UmbracoCommunity.Web.Models.ViewModels.Components;

public class MetaTagsViewModel
{
    private readonly List<string> _schemaMarkups = [];

    public string Name { get; set; } = string.Empty;

    public string? SiteName { get; set; }

    public string MetaTitle { get; set; } = string.Empty;

    public string MetaDescription { get; set; } = string.Empty;

    public string OpenGraphImageUrl { get; set; } = string.Empty;

    /// <summary>The <c>og:type</c> value. Pages are "website" unless a page says otherwise.</summary>
    public string OpenGraphType { get; set; } = "website";

    /// <summary>The <c>twitter:card</c> value.</summary>
    public string TwitterCard { get; set; } = "summary_large_image";

    /// <summary>Declared size of the share image. Null leaves the size tags out, for an image whose size is not fixed.</summary>
    public (int Width, int Height)? OpenGraphImageSize { get; set; } = (1200, 628);

    public bool HasOpenGraphImageUrl => !string.IsNullOrWhiteSpace(OpenGraphImageUrl);

    public string Robots { get; set; } = string.Empty;

    public string? CanonicalUrl { get; set; }

    public string? NextUrl { get; set; }

    public string? PrevUrl { get; set; }

    public IReadOnlyCollection<string> SchemaMarkups => _schemaMarkups.AsReadOnly();

    public void AddSchemaMarkup(string markup) => _schemaMarkups.Add(markup);
}

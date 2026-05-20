namespace UmbracoCommunity.Web.Services.Documentation;

public interface IDocumentationService
{
    /// <summary>
    /// Returns the full documentation index (all sections, subsections, and articles).
    /// </summary>
    DocumentationIndex GetIndex();

    /// <summary>
    /// Resolves a URL path (segments after the Documentation node) to either an article or a section.
    /// </summary>
    DocumentationResolution Resolve(IReadOnlyList<string> pathSegments);
}

public readonly record struct DocumentationResolution(DocumentationSection? Section, DocumentationArticle? Article)
{
    public static DocumentationResolution NotFound { get; } = new(null, null);

    public bool IsNotFound => Section is null && Article is null;
}

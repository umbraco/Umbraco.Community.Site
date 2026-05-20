namespace UmbracoCommunity.Web.Services.Documentation;

public sealed record DocumentationIndex(IReadOnlyList<DocumentationSection> Sections)
{
    public static DocumentationIndex Empty { get; } = new(Array.Empty<DocumentationSection>());

    public bool HasContent => Sections.Count > 0;
}

public sealed record DocumentationSection(
    string Title,
    string Slug,
    IReadOnlyList<string> PathSegments,
    string Url,
    IReadOnlyList<DocumentationSection> Subsections,
    IReadOnlyList<DocumentationArticle> Articles,
    DocumentationArticle? Intro)
{
    public bool IsEmpty => Subsections.Count == 0 && Articles.Count == 0 && Intro is null;
}

public sealed record DocumentationArticle(
    string Title,
    string Slug,
    IReadOnlyList<string> PathSegments,
    string Url,
    string HtmlContent,
    string? Excerpt,
    DateTime LastModifiedUtc,
    IReadOnlyList<string> SectionPathSegments,
    bool IsSectionIntro);

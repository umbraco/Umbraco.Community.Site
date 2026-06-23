using Examine;
using Examine.Lucene;
using Microsoft.Extensions.Options;

namespace UmbracoCommunity.Web.Services.Documentation.Search;

public sealed class ConfigureDocumentationIndexOptions : IConfigureNamedOptions<LuceneDirectoryIndexOptions>
{
    public void Configure(string? name, LuceneDirectoryIndexOptions options)
    {
        if (!string.Equals(name, DocumentationLuceneIndex.IndexName, StringComparison.Ordinal))
        {
            return;
        }

        options.FieldDefinitions = new FieldDefinitionCollection(
            new FieldDefinition(DocumentationIndexFields.Title, FieldDefinitionTypes.FullText),
            new FieldDefinition(DocumentationIndexFields.Tags, FieldDefinitionTypes.FullText),
            new FieldDefinition(DocumentationIndexFields.Tokens, FieldDefinitionTypes.FullText),
            new FieldDefinition(DocumentationIndexFields.Excerpt, FieldDefinitionTypes.FullText),
            new FieldDefinition(DocumentationIndexFields.Body, FieldDefinitionTypes.FullText),
            new FieldDefinition(DocumentationIndexFields.Section, FieldDefinitionTypes.FullText),
            new FieldDefinition(DocumentationIndexFields.ArticlePath, FieldDefinitionTypes.Raw),
            new FieldDefinition(DocumentationIndexFields.SectionPath, FieldDefinitionTypes.Raw));
    }

    public void Configure(LuceneDirectoryIndexOptions options) => Configure(string.Empty, options);
}

public static class DocumentationIndexFields
{
    public const string Title = "title";
    /// <summary>
    /// Curated tags from the article's YAML frontmatter. Boosted heavily — when an author tagged a doc
    /// with a term, that's the strongest possible relevance signal.
    /// </summary>
    public const string Tags = "tags";
    /// <summary>
    /// Synthetic field holding slug parts plus their consecutive concatenations
    /// (e.g. "inline-svg-tag-helper" → "inline svg tag helper inlinesvg svgtag taghelper inlinesvgtag svgtaghelper inlinesvgtaghelper").
    /// Lets searches like "taghelper" hit articles whose only signal lives in a hyphenated slug.
    /// </summary>
    public const string Tokens = "tokens";
    public const string Excerpt = "excerpt";
    public const string Body = "body";
    public const string Section = "section";
    public const string ArticlePath = "articlePath";
    public const string SectionPath = "sectionPath";
}

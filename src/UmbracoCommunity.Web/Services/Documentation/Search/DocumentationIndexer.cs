using System.Text;
using System.Text.RegularExpressions;
using Examine;
using Microsoft.Extensions.Logging;

namespace UmbracoCommunity.Web.Services.Documentation.Search;

public sealed class DocumentationIndexer : IDocumentationIndexer, IDisposable
{
    private static readonly Regex HtmlTagPattern = new("<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex WhitespacePattern = new(@"\s+", RegexOptions.Compiled);
    // Match <pre>...</pre> across lines so we can drop multi-line code blocks before tokenising the body.
    // Code blocks are noisy for content search (file extensions, API names, etc. drag in unrelated hits).
    // Inline <code> spans are kept — they often contain filenames/types users legitimately search for.
    private static readonly Regex PreBlockPattern = new("<pre\\b[^>]*>.*?</pre>", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);

    private readonly IExamineManager _examineManager;
    private readonly IDocumentationService _documentationService;
    private readonly ILogger<DocumentationIndexer> _logger;
    private readonly object _rebuildLock = new();

    public DocumentationIndexer(
        IExamineManager examineManager,
        IDocumentationService documentationService,
        ILogger<DocumentationIndexer> logger)
    {
        _examineManager = examineManager;
        _documentationService = documentationService;
        _logger = logger;
        _documentationService.IndexRebuilt += OnDocsIndexRebuilt;
    }

    private void OnDocsIndexRebuilt(object? sender, EventArgs e) => RebuildIndex();

    public void Dispose() => _documentationService.IndexRebuilt -= OnDocsIndexRebuilt;

    public void RebuildIndex()
    {
        lock (_rebuildLock)
        {
            if (!_examineManager.TryGetIndex(DocumentationLuceneIndex.IndexName, out var index))
            {
                _logger.LogWarning("DocumentationIndexer: index '{IndexName}' is not registered.",
                    DocumentationLuceneIndex.IndexName);
                return;
            }

            var docs = _documentationService.GetIndex();
            var articles = EnumerateArticles(docs).ToList();

            index.CreateIndex(); // truncate
            if (articles.Count == 0)
            {
                _logger.LogInformation("DocumentationIndexer: no articles to index.");
                return;
            }

            var valueSets = articles.Select(BuildValueSet).ToList();
            index.IndexItems(valueSets);

            _logger.LogInformation("DocumentationIndexer: indexed {Count} article(s) into '{IndexName}'.",
                valueSets.Count, DocumentationLuceneIndex.IndexName);
        }
    }

    private static IEnumerable<(DocumentationArticle Article, DocumentationSection Section)> EnumerateArticles(DocumentationIndex docs)
    {
        foreach (var section in docs.Sections)
        {
            foreach (var article in section.Articles)
            {
                yield return (article, section);
            }
            foreach (var sub in section.Subsections)
            {
                foreach (var article in sub.Articles)
                {
                    yield return (article, sub);
                }
                // One level of nesting is all we currently produce — extend here if deeper trees appear.
            }
        }
    }

    private static ValueSet BuildValueSet((DocumentationArticle Article, DocumentationSection Section) item)
    {
        var (article, section) = item;
        var body = StripHtml(article.HtmlContent);
        var values = new Dictionary<string, object>
        {
            [DocumentationIndexFields.Title] = article.Title,
            [DocumentationIndexFields.Tags] = string.Join(' ', article.Tags),
            [DocumentationIndexFields.Tokens] = BuildTokens(article, section),
            [DocumentationIndexFields.Excerpt] = article.Excerpt ?? string.Empty,
            [DocumentationIndexFields.Body] = body,
            [DocumentationIndexFields.Section] = section.Title,
            [DocumentationIndexFields.ArticlePath] = string.Join('/', article.PathSegments),
            [DocumentationIndexFields.SectionPath] = string.Join('/', section.PathSegments),
        };
        var id = string.Join('/', article.PathSegments);
        return new ValueSet(id, "documentation", values);
    }

    /// <summary>
    /// Builds a synthetic token bag from the article slug and title. We emit:
    ///   - each individual word (already covered by other fields, but harmless and useful for ranking)
    ///   - every consecutive concatenation of slug parts (2-gram, 3-gram, …) so partial matches
    ///     like "taghelper" hit "tag-helper".
    /// All lowercased; output is space-joined so Lucene's standard analyzer treats each as a separate token.
    /// </summary>
    private static string BuildTokens(DocumentationArticle article, DocumentationSection section)
    {
        var slugParts = article.Slug.Split('-', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.ToLowerInvariant())
            .ToArray();

        var titleWords = SplitWords(article.Title);
        var sectionWords = SplitWords(section.Title);

        var tokens = new HashSet<string>(StringComparer.Ordinal);
        foreach (var w in titleWords) tokens.Add(w);
        foreach (var w in sectionWords) tokens.Add(w);
        foreach (var p in slugParts) tokens.Add(p);

        // Concatenations of consecutive slug parts: 2-gram, 3-gram, …, n-gram.
        for (var window = 2; window <= slugParts.Length; window++)
        {
            for (var i = 0; i <= slugParts.Length - window; i++)
            {
                tokens.Add(string.Concat(slugParts.AsSpan(i, window).ToArray()));
            }
        }

        return string.Join(' ', tokens);
    }

    private static IEnumerable<string> SplitWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) yield break;
        var current = new StringBuilder();
        foreach (var c in text)
        {
            if (char.IsLetterOrDigit(c))
            {
                current.Append(char.ToLowerInvariant(c));
            }
            else if (current.Length > 0)
            {
                yield return current.ToString();
                current.Clear();
            }
        }
        if (current.Length > 0) yield return current.ToString();
    }

    private static string StripHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var stripped = PreBlockPattern.Replace(html, " ");
        var text = HtmlTagPattern.Replace(stripped, " ");
        text = System.Net.WebUtility.HtmlDecode(text);
        text = WhitespacePattern.Replace(text, " ").Trim();
        return text;
    }
}

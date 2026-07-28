using System.Text;
using Examine;
using Examine.Search;
using Microsoft.Extensions.Logging;

namespace UmbracoCommunity.Web.Services.Documentation.Search;

public sealed class DocumentationSearchService : IDocumentationSearchService
{
    /// <summary>
    /// Keep hits whose score is at least this fraction of the top hit's score. Filters the long tail
    /// of "matched one token in the body" results that Lucene still ranks above zero.
    /// </summary>
    private const double MinScoreRatio = 0.33;

    private readonly IExamineManager _examineManager;
    private readonly ILogger<DocumentationSearchService> _logger;

    public DocumentationSearchService(
        IExamineManager examineManager,
        ILogger<DocumentationSearchService> logger)
    {
        _examineManager = examineManager;
        _logger = logger;
    }

    public IReadOnlyList<DocumentationSearchHit> Search(string query, int maxResults)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<DocumentationSearchHit>();
        }

        if (!_examineManager.TryGetIndex(DocumentationLuceneIndex.IndexName, out var index))
        {
            _logger.LogWarning("DocumentationSearchService: index '{IndexName}' not registered.",
                DocumentationLuceneIndex.IndexName);
            return Array.Empty<DocumentationSearchHit>();
        }

        var luceneQuery = BuildLuceneQuery(query);
        if (string.IsNullOrEmpty(luceneQuery))
        {
            return Array.Empty<DocumentationSearchHit>();
        }

        try
        {
            var searchResults = index.Searcher
                .CreateQuery("documentation", BooleanOperation.Or)
                .NativeQuery(luceneQuery)
                .Execute(QueryOptions.SkipTake(0, maxResults))
                .ToList();

            if (searchResults.Count == 0)
            {
                return Array.Empty<DocumentationSearchHit>();
            }

            var topScore = searchResults[0].Score;
            var threshold = topScore * MinScoreRatio;

            return searchResults
                .Where(r => r.Score >= threshold)
                .Select(MapResult)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DocumentationSearchService: query '{Query}' failed.", query);
            return Array.Empty<DocumentationSearchHit>();
        }
    }

    /// <summary>
    /// Tokenises the user input and builds a multi-field boosted Lucene query. Title matches outweigh
    /// excerpt which outweighs body; each term also matches as a prefix so partial words ("tag")
    /// surface hits on "tag-helper" / "tags".
    /// </summary>
    private static string BuildLuceneQuery(string rawQuery)
    {
        // Split on whitespace + punctuation, then ALSO split any remaining hyphens so "tag-helper"
        // produces ["tag", "helper"] alongside "taghelper" (the original form is preserved below).
        var rawTerms = rawQuery
            .Split([' ', '\t', '\r', '\n', ',', '.', ';', ':', '!', '?', '/', '\\', '(', ')', '[', ']', '{', '}', '"', '\''], StringSplitOptions.RemoveEmptyEntries);

        var terms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in rawTerms)
        {
            var combined = SanitiseTerm(raw, keepHyphens: false);
            if (combined.Length > 0) terms.Add(combined);
            foreach (var piece in raw.Split('-', StringSplitOptions.RemoveEmptyEntries))
            {
                var sanitised = SanitiseTerm(piece, keepHyphens: false);
                if (sanitised.Length > 0) terms.Add(sanitised);
            }
        }

        if (terms.Count == 0)
        {
            return string.Empty;
        }

        var clauses = new StringBuilder();
        foreach (var term in terms)
        {
            if (clauses.Length > 0) clauses.Append(' ');
            clauses.Append('(');
            clauses.Append($"{DocumentationIndexFields.Tags}:{term}^10 ");
            clauses.Append($"{DocumentationIndexFields.Tags}:{term}*^6 ");
            clauses.Append($"{DocumentationIndexFields.Title}:{term}^5 ");
            clauses.Append($"{DocumentationIndexFields.Title}:{term}*^3 ");
            clauses.Append($"{DocumentationIndexFields.Tokens}:{term}^4 ");
            clauses.Append($"{DocumentationIndexFields.Tokens}:{term}*^2 ");
            clauses.Append($"{DocumentationIndexFields.Excerpt}:{term}^3 ");
            clauses.Append($"{DocumentationIndexFields.Section}:{term}^2 ");
            clauses.Append($"{DocumentationIndexFields.Body}:{term}^1");
            clauses.Append(')');
        }
        return clauses.ToString();
    }

    private static string SanitiseTerm(string term, bool keepHyphens)
    {
        var sb = new StringBuilder(term.Length);
        foreach (var c in term)
        {
            if (char.IsLetterOrDigit(c) || c == '_' || (keepHyphens && c == '-'))
            {
                sb.Append(char.ToLowerInvariant(c));
            }
        }
        return sb.ToString();
    }

    private static DocumentationSearchHit MapResult(ISearchResult result)
    {
        var title = result.Values.TryGetValue(DocumentationIndexFields.Title, out var t) ? t : string.Empty;
        var section = result.Values.TryGetValue(DocumentationIndexFields.Section, out var s) ? s : string.Empty;
        var articlePath = result.Values.TryGetValue(DocumentationIndexFields.ArticlePath, out var p) ? p : result.Id;
        var excerpt = result.Values.TryGetValue(DocumentationIndexFields.Excerpt, out var e) ? e : null;
        return new DocumentationSearchHit(title, section, articlePath, string.IsNullOrWhiteSpace(excerpt) ? null : excerpt);
    }
}

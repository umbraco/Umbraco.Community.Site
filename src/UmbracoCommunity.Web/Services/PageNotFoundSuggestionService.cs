using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Models.PublishedContent;
using UmbracoCommunity.Web.Abstract.Services;

namespace UmbracoCommunity.Web.Services;

internal sealed class PageNotFoundSuggestionService : IPageNotFoundSuggestionService
{
    // Scanners probe these; real Umbraco content URLs are extensionless. ".zip" stays allowed for /seed/latest.zip.
    private static readonly HashSet<string> NonContentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".php", ".php5", ".php7", ".asp", ".aspx", ".jsp", ".jspx", ".cgi", ".pl", ".py", ".rb",
        ".sh", ".env", ".ini", ".conf", ".config", ".yml", ".yaml", ".json", ".xml", ".sql",
        ".bak", ".old", ".swp", ".tar", ".gz", ".rar", ".7z", ".log", ".db", ".git",
    };

    private readonly IContentSuggestionService _suggestions;
    private readonly ILogger<PageNotFoundSuggestionService> _logger;

    public PageNotFoundSuggestionService(IContentSuggestionService suggestions, ILogger<PageNotFoundSuggestionService> logger)
    {
        _suggestions = suggestions;
        _logger = logger;
    }

    public Task<IReadOnlyList<IPublishedContent>> GetSuggestionsAsync(
        IPublishedContent currentPage,
        string requestedPath,
        string? referrerUrl,
        int max,
        CancellationToken ct)
    {
        if (max <= 0 || HasNonContentExtension(requestedPath))
        {
            return Task.FromResult<IReadOnlyList<IPublishedContent>>(Array.Empty<IPublishedContent>());
        }

        var query = BuildQuery(requestedPath, referrerUrl);
        if (string.IsNullOrWhiteSpace(query))
        {
            _logger.LogDebug("PageNotFound suggestions: empty query for path '{Path}' (no usable tokens)", requestedPath);
            return Task.FromResult<IReadOnlyList<IPublishedContent>>(Array.Empty<IPublishedContent>());
        }

        return _suggestions.GetSuggestionsAsync(currentPage, query, max, ct);
    }

    // True when the last path segment has a non-content extension such as ".php" or ".env". Internal for unit testing.
    internal static bool HasNonContentExtension(string path)
    {
        if (string.IsNullOrEmpty(path)) return false;

        var lastSlash = path.LastIndexOf('/');
        var lastSegment = lastSlash >= 0 ? path[(lastSlash + 1)..] : path;

        var dot = lastSegment.LastIndexOf('.');
        // dot == 0 is allowed: for dotfiles like ".env" the whole segment is the extension.
        if (dot < 0 || dot == lastSegment.Length - 1) return false;

        return NonContentExtensions.Contains(lastSegment[dot..]);
    }

    internal static string BuildQuery(string requestedPath, string? referrerUrl)
    {
        var tokens = new List<string>();
        tokens.AddRange(TokensFromPath(requestedPath));

        if (!string.IsNullOrEmpty(referrerUrl)
            && Uri.TryCreate(referrerUrl, UriKind.Absolute, out var referrerUri))
        {
            tokens.AddRange(TokensFromPath(referrerUri.AbsolutePath));
        }

        return string.Join(' ', tokens.Distinct(StringComparer.OrdinalIgnoreCase));
    }

    // Short fragments and pure numbers add noise to embeddings.
    private static IEnumerable<string> TokensFromPath(string path)
    {
        if (string.IsNullOrEmpty(path)) yield break;
        foreach (var raw in path.Split(['/', '-', '_', '.'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (raw.Length <= 1) continue;
            if (raw.All(char.IsDigit)) continue;
            yield return raw;
        }
    }
}

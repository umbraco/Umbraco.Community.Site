using System.Globalization;
using System.Text;

namespace UmbracoCommunity.Web.Services;

/// <summary>
/// Builds a Lucene query that matches each word exactly, by its root ("speaking" finds "speakers") and with
/// small typos ("tickts" finds "tickets"), ranking exact and title matches highest.
/// </summary>
internal static class SearchQueryBuilder
{
    internal const float ExactBoost = 3f;
    internal const float RootBoost = 1.5f;
    internal const float FuzzyBoost = 0.5f;
    internal const float TitleBoost = 3f;
    private const int MinRootLength = 3;
    private const int MinFuzzyLength = 5;
    private const int LongWordLength = 8;
    private const int MaxTerms = 6;

    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
    {
        "a", "an", "and", "are", "as", "at", "be", "by", "can", "do", "does", "for", "from", "how", "i", "if",
        "in", "is", "it", "me", "my", "of", "on", "or", "the", "there", "to", "was", "what", "when", "where",
        "which", "who", "why", "will", "with", "you", "your",
    };

    private static readonly string[] Suffixes = ["ings", "ing", "ers", "er", "ies", "es", "ed", "s"];

    /// <summary>
    /// Returns null when the query has no searchable words, so callers can fall back to a plain query.
    /// Typo matching only runs on <paramref name="fuzzyFields"/>, as it is the costliest clause on long fields.
    /// </summary>
    internal static string? Build(string query, IReadOnlyList<string> fields, string titleField, IReadOnlyCollection<string> fuzzyFields)
    {
        var terms = Terms(query);
        if (terms.Count == 0) return null;

        var clauses = new List<string>();
        foreach (var term in terms)
        {
            var root = Root(term);
            var fuzzy = term.Length >= MinFuzzyLength ? (term.Length >= LongWordLength ? 2 : 1) : 0;

            foreach (var field in fields)
            {
                var weight = field == titleField ? TitleBoost : 1f;
                clauses.Add($"{field}:{term}^{Format(ExactBoost * weight)}");
                if (root.Length >= MinRootLength)
                {
                    clauses.Add($"{field}:{root}*^{Format(RootBoost * weight)}");
                }
                if (fuzzy > 0 && fuzzyFields.Contains(field))
                {
                    clauses.Add($"{field}:{term}~{fuzzy}^{Format(FuzzyBoost * weight)}");
                }
            }
        }

        return string.Join(' ', clauses);
    }

    internal static IReadOnlyList<string> Terms(string query)
    {
        var words = new List<string>();
        var current = new StringBuilder();
        foreach (var c in Fold(query))
        {
            if (char.IsLetterOrDigit(c))
            {
                current.Append(char.ToLowerInvariant(c));
            }
            else
            {
                Flush();
            }
        }
        Flush();
        return words.Distinct().Take(MaxTerms).ToList();

        void Flush()
        {
            if (current.Length > 1 && !StopWords.Contains(current.ToString())) words.Add(current.ToString());
            current.Clear();
        }
    }

    // A light English stem: enough for "speaking"/"speakers" -> "speak" and "tickets" -> "ticket".
    internal static string Root(string term)
    {
        foreach (var suffix in Suffixes)
        {
            if (term.Length - suffix.Length >= 4 && term.EndsWith(suffix, StringComparison.Ordinal))
            {
                return term[..^suffix.Length];
            }
        }
        return term;
    }

    // Prefix and fuzzy terms bypass the index analyzer, so fold to match its ASCII-folded tokens.
    private static string Fold(string text)
    {
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            if (Letters.TryGetValue(c, out var replacement)) builder.Append(replacement);
            else builder.Append(c);
        }
        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    // Letters that don't decompose into a base letter plus accent.
    private static readonly Dictionary<char, string> Letters = new()
    {
        ['ø'] = "o", ['Ø'] = "O", ['æ'] = "ae", ['Æ'] = "AE", ['ß'] = "ss", ['đ'] = "d", ['Đ'] = "D",
        ['ł'] = "l", ['Ł'] = "L", ['œ'] = "oe", ['Œ'] = "OE", ['þ'] = "th", ['Þ'] = "TH", ['ð'] = "d", ['Ð'] = "D",
    };

    private static string Format(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace UmbracoCommunity.SearchTracker.Recording;

/// <summary>
/// Turns a raw query into the stored term: lower-cased, whitespace collapsed, length capped,
/// with email addresses and phone-like numbers masked so personal data isn't kept.
/// </summary>
public static partial class SearchTermNormalizer
{
    public const string EmailPlaceholder = "[email]";
    public const string NumberPlaceholder = "[number]";
    private const int MinDigitsToMask = 7;

    public static string? Normalize(string? raw, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(raw) || maxLength <= 0) return null;

        var builder = new StringBuilder(raw.Length);
        foreach (var c in raw.Normalize(NormalizationForm.FormKC))
        {
            if (char.IsControl(c)) builder.Append(' ');
            else builder.Append(c);
        }

        var text = builder.ToString().ToLowerInvariant();
        text = EmailRegex().Replace(text, EmailPlaceholder);
        text = NumberRegex().Replace(text, m => LooksLikePhoneOrId(m.Value) ? NumberPlaceholder : m.Value);
        text = WhitespaceRegex().Replace(text, " ").Trim();

        if (text.Length > maxLength)
        {
            text = text[..maxLength].TrimEnd();
        }

        return text.Length == 0 ? null : text;
    }

    // Years and version lists ("2026 2027", "13 14") stay; a leading +, a long digit run or 3+ short groups ("12 34 56 78") don't.
    private static bool LooksLikePhoneOrId(string candidate)
    {
        var groups = DigitGroupRegex().Matches(candidate).Select(g => g.Length).ToList();
        if (groups.Sum() < MinDigitsToMask) return false;
        return candidate.Contains('+') || groups.Max() >= MinDigitsToMask || (groups.Count >= 3 && groups.All(g => g <= 4));
    }

    [GeneratedRegex(@"\d+", RegexOptions.CultureInvariant)]
    private static partial Regex DigitGroupRegex();

    [GeneratedRegex(@"[^\s@]+@[^\s@]+\.[^\s@]+", RegexOptions.CultureInvariant)]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"[+(]*\d[\d \-()]*\d", RegexOptions.CultureInvariant)]
    private static partial Regex NumberRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();
}

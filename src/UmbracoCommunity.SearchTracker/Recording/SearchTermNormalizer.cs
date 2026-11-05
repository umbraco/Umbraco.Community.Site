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
        text = NumberRegex().Replace(text, m => m.Value.Count(char.IsAsciiDigit) >= MinDigitsToMask ? NumberPlaceholder : m.Value);
        text = WhitespaceRegex().Replace(text, " ").Trim();

        if (text.Length > maxLength)
        {
            text = text[..maxLength].TrimEnd();
        }

        return text.Length == 0 ? null : text;
    }

    [GeneratedRegex(@"[^\s@]+@[^\s@]+\.[^\s@]+", RegexOptions.CultureInvariant)]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"\+?\d[\d \-().]*\d", RegexOptions.CultureInvariant)]
    private static partial Regex NumberRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();
}

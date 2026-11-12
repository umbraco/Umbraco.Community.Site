using System.Text;
using System.Text.RegularExpressions;

namespace UmbracoCommunity.Web.Services;

/// <summary>
/// Strips values that block editors store for pickers and data-only properties (JSON, GUIDs, <c>umb://</c>
/// references) from indexed block text, so they're neither searchable nor shown as excerpts.
/// </summary>
internal static class SearchIndexText
{
    private static readonly Regex GuidRegex = new(
        @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b", RegexOptions.Compiled);
    private static readonly Regex UdiRegex = new(@"umb://[a-z-]+/[0-9a-fA-F]{32}", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

    public static string Clean(string value)
    {
        var text = RemoveJson(value);
        text = UdiRegex.Replace(text, " ");
        text = GuidRegex.Replace(text, " ");
        return WhitespaceRegex.Replace(text, " ").Trim();
    }

    private static string RemoveJson(string value)
    {
        if (value.IndexOfAny(['{', '[']) < 0) return value;

        var output = new StringBuilder(value.Length);
        var i = 0;
        while (i < value.Length)
        {
            if (value[i] is '{' or '[' && LooksLikeJson(value, i) && TryFindJsonEnd(value, i, out var end))
            {
                output.Append(' ');
                i = end + 1;
                continue;
            }

            output.Append(value[i]);
            i++;
        }

        return output.ToString();
    }

    // Editor JSON opens with a quoted key or a nested value; prose in brackets doesn't.
    private static bool LooksLikeJson(string value, int start)
    {
        var close = value[start] == '{' ? '}' : ']';
        for (var i = start + 1; i < value.Length; i++)
        {
            if (char.IsWhiteSpace(value[i])) continue;
            return value[i] is '"' or '{' or '[' || value[i] == close;
        }

        return false;
    }

    private static bool TryFindJsonEnd(string value, int start, out int end)
    {
        var depth = 0;
        var inString = false;
        for (var i = start; i < value.Length; i++)
        {
            var c = value[i];
            if (inString)
            {
                if (c == '\\') i++;
                else if (c == '"') inString = false;
                continue;
            }

            switch (c)
            {
                case '"':
                    inString = true;
                    break;
                case '{' or '[':
                    depth++;
                    break;
                case '}' or ']':
                    depth--;
                    if (depth == 0)
                    {
                        end = i;
                        return true;
                    }
                    break;
            }
        }

        end = -1;
        return false;
    }
}

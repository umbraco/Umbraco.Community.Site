namespace Umbraco.Community.NotFoundTracker.Matching;

public static class UrlNormalizer
{
    public const int MaxPathLength = 2048;

    public static string NormalizePath(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return "/";
        }

        // Lower-case first to keep all comparisons case-insensitive.
        var lower = path.ToLowerInvariant();

        // Ensure leading slash, then collapse runs of consecutive slashes.
        if (!lower.StartsWith('/'))
        {
            lower = "/" + lower;
        }

        var collapsed = CollapseSlashes(lower);

        return collapsed.Length > MaxPathLength
            ? collapsed[..MaxPathLength]
            : collapsed;
    }

    public static string NormalizeHostname(string? hostname)
    {
        return string.IsNullOrEmpty(hostname) ? string.Empty : hostname.ToLowerInvariant();
    }

    private static string CollapseSlashes(string input)
    {
        if (!input.Contains("//"))
        {
            return input;
        }

        var sb = new System.Text.StringBuilder(input.Length);
        var prevWasSlash = false;
        foreach (var ch in input)
        {
            if (ch == '/')
            {
                if (!prevWasSlash)
                {
                    sb.Append(ch);
                }
                prevWasSlash = true;
            }
            else
            {
                sb.Append(ch);
                prevWasSlash = false;
            }
        }
        return sb.ToString();
    }
}

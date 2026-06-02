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
        if (string.IsNullOrEmpty(hostname))
        {
            return string.Empty;
        }

        var lower = hostname.ToLowerInvariant();

        // Strip scheme so "https://example.com" and "example.com" collapse to one bucket.
        if (lower.StartsWith("https://", StringComparison.Ordinal))
        {
            lower = lower[8..];
        }
        else if (lower.StartsWith("http://", StringComparison.Ordinal))
        {
            lower = lower[7..];
        }

        // Strip trailing slash(es) so "example.com/" and "example.com" match.
        return lower.TrimEnd('/');
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

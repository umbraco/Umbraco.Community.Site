namespace UmbracoCommunity.Web.Extensions;

public static class LinkExtensions
{
    public const string NewTabTarget = "_blank";
    public const string NewTabRelValue = "noopener noreferrer";

    public static bool OpensInNewTab(this string? target)
        => string.Equals(target, NewTabTarget, StringComparison.Ordinal);

    public static string? NewTabRel(this string? target)
        => target.OpensInNewTab() ? NewTabRelValue : null;
}

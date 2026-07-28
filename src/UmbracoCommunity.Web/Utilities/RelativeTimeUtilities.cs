namespace UmbracoCommunity.Web.Utilities;

/// <summary>Formats a past instant as a human-readable relative time (e.g. "3 hours ago").</summary>
public static class RelativeTimeUtilities
{
    public static string ToRelativeTime(this DateTimeOffset value)
    {
        var span = DateTimeOffset.UtcNow - value;
        if (span.TotalDays >= 365) { var y = (int)(span.TotalDays / 365); return $"{y} year{(y == 1 ? "" : "s")} ago"; }
        if (span.TotalDays >= 30) { var m = (int)(span.TotalDays / 30); return $"{m} month{(m == 1 ? "" : "s")} ago"; }
        if (span.TotalDays >= 7) { var w = (int)(span.TotalDays / 7); return $"{w} week{(w == 1 ? "" : "s")} ago"; }
        if (span.TotalDays >= 1) { var d = (int)span.TotalDays; return $"{d} day{(d == 1 ? "" : "s")} ago"; }
        if (span.TotalHours >= 1) { var h = (int)span.TotalHours; return $"{h} hour{(h == 1 ? "" : "s")} ago"; }
        var mins = Math.Max(1, (int)span.TotalMinutes);
        return $"{mins} minute{(mins == 1 ? "" : "s")} ago";
    }
}

using System.Globalization;

namespace UmbracoCommunity.Web.Features.Profiles;

/// <summary>
/// The profile's activity graph: the last 12 months, one cell per week (weeks start on Monday, UTC), shaded by how
/// much was going on that week. Built from the timeline's dates. The levels are fixed steps, not relative to
/// anyone else, and the graph carries a per-month text alternative so it never relies on colour alone.
/// </summary>
/// <remarks>Shown unless the member switches it off (see <see cref="Models.ProfileDisplayChoices.HideActivityGraph"/>).</remarks>
/// <param name="Weeks">The weeks, oldest first. The first starts on the Monday on or before the window's start.</param>
/// <param name="Months">The months in the window, oldest first, with how many items each had.</param>
public sealed record ProfileActivityGraph(
    IReadOnlyList<ProfileActivityGraphWeek> Weeks,
    IReadOnlyList<ProfileActivityGraphMonth> Months)
{
    public const int MaxLevel = 4;

    /// <summary>True when nothing happened in the window; the graph is not shown then.</summary>
    public bool IsEmpty => Weeks.All(w => w.Count == 0);

    /// <summary>
    /// Buckets <paramref name="dates"/> into the weeks of the 12 months up to and including <paramref name="today"/>.
    /// Dates outside the window are ignored.
    /// </summary>
    public static ProfileActivityGraph Build(IEnumerable<DateTimeOffset> dates, DateOnly today)
    {
        var windowStart = today.AddMonths(-12).AddDays(1);
        var firstWeek = windowStart.AddDays(-DaysSinceMonday(windowStart));

        var days = dates
            .Select(d => DateOnly.FromDateTime(d.UtcDateTime))
            .Where(d => d >= windowStart && d <= today)
            .ToList();

        var weeks = new List<ProfileActivityGraphWeek>();
        for (var start = firstWeek; start <= today; start = start.AddDays(7))
        {
            var end = start.AddDays(6);
            var count = days.Count(d => d >= start && d <= end);
            var monthStart = start.Day == 1 || end.Day < start.Day ? new DateOnly(end.Year, end.Month, 1) : (DateOnly?)null;
            weeks.Add(new ProfileActivityGraphWeek(start, count, Level(count))
            {
                MonthStart = monthStart?.ToString("MMM", CultureInfo.InvariantCulture),
            });
        }

        var months = new List<ProfileActivityGraphMonth>();
        for (var month = new DateOnly(windowStart.Year, windowStart.Month, 1); month <= today; month = month.AddMonths(1))
        {
            var count = days.Count(d => d.Year == month.Year && d.Month == month.Month);
            months.Add(new ProfileActivityGraphMonth(ProfileTimeline.MonthLabel(month.Year, month.Month), count));
        }

        return new ProfileActivityGraph(weeks, months);
    }

    /// <summary>How a week is shaded: 0 for nothing, then 1, 2, 3–4, and 5 or more items.</summary>
    public static int Level(int count) => count switch
    {
        <= 0 => 0,
        1 => 1,
        2 => 2,
        <= 4 => 3,
        _ => MaxLevel,
    };

    private static int DaysSinceMonday(DateOnly date) => ((int)date.DayOfWeek + 6) % 7;
}

/// <summary>A week of the activity graph, starting on <paramref name="Start"/> (a Monday).</summary>
public sealed record ProfileActivityGraphWeek(DateOnly Start, int Count, int Level)
{
    /// <summary>The three-letter name of the month whose first day falls in this week, e.g. "Mar"; otherwise null.</summary>
    public string? MonthStart { get; init; }
}

/// <summary>A month of the activity graph's text alternative, e.g. "Mar 2026".</summary>
public sealed record ProfileActivityGraphMonth(string Label, int Count);

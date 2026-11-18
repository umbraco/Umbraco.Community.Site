namespace UmbracoCommunity.Web.Features.OpenBadges.Models;

/// <summary>A member's MVP years, newest first (empty when never an MVP), and whether the newest is the current MVP year.</summary>
public sealed record MvpStatus(IReadOnlyList<int> Years, bool IsCurrent)
{
    public static readonly MvpStatus None = new([], false);

    /// <summary>The member's current MVP year, or null when they aren't a current MVP.</summary>
    public int? CurrentYear => IsCurrent && Years.Count > 0 ? Years[0] : null;
}

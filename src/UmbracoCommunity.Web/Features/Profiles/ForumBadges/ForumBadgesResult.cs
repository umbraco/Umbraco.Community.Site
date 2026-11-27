namespace UmbracoCommunity.Web.Features.Profiles.ForumBadges;

/// <summary>What looking up a forum user's badges came to.</summary>
public enum ForumBadgesStatus
{
    /// <summary>The forum answered; <see cref="ForumBadgesResult.Badges"/> holds the user's badges (possibly none).</summary>
    Ok = 0,

    /// <summary>The forum has no such user.</summary>
    NotFound = 1,

    /// <summary>The forum could not be reached or gave an answer we could not read. Worth trying again later.</summary>
    Unavailable = 2,
}

/// <summary>The badges of one forum user, or why there are none to show.</summary>
public sealed record ForumBadgesResult(ForumBadgesStatus Status, IReadOnlyList<ForumBadge> Badges)
{
    public static ForumBadgesResult Ok(IReadOnlyList<ForumBadge> badges) => new(ForumBadgesStatus.Ok, badges);

    public static readonly ForumBadgesResult NotFound = new(ForumBadgesStatus.NotFound, []);

    public static readonly ForumBadgesResult Unavailable = new(ForumBadgesStatus.Unavailable, []);
}

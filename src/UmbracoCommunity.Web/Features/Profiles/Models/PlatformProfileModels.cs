namespace UmbracoCommunity.Web.Features.Profiles.Models;

/// <summary>A member's profile on the external content platform.</summary>
public sealed record PlatformProfile(string Id, IReadOnlyList<int> MvpYears)
{
    /// <summary>Accounts on other platforms that the platform ties to this profile.</summary>
    public IReadOnlyList<PlatformIdentity> Identities { get; init; } = [];

    /// <summary>Feeds that the platform ties to this profile.</summary>
    public IReadOnlyList<PlatformFeed> Feeds { get; init; } = [];
}

/// <summary>An account on another platform (e.g. a social network) that the content platform ties to a profile.</summary>
public sealed record PlatformIdentity(string? Platform, string? Username, string ProfileUrl);

/// <summary>A feed (blog, podcast, …) that the content platform ties to a profile.</summary>
public sealed record PlatformFeed(string Url, string? Name);

/// <summary>What the content platform knows about one link, see <see cref="PlatformLinkStatuses"/>.</summary>
public sealed record PlatformLinkCheck(string Url, string Status, string? Platform);

/// <summary>The answers the content platform gives when asked about a link.</summary>
public static class PlatformLinkStatuses
{
    /// <summary>The platform already ties the account behind the link to the same profile.</summary>
    public const string Confirmed = "confirmed";

    /// <summary>The platform ties the account behind the link to a different profile.</summary>
    public const string Conflict = "conflict";

    /// <summary>The platform knows nothing about the link.</summary>
    public const string Unknown = "unknown";

    /// <summary>The platform cannot check this kind of link.</summary>
    public const string Unsupported = "unsupported";
}

/// <summary>One public activity from the external content platform, as it reports it.</summary>
public sealed record PlatformActivity(
    string Id,
    string Type,
    string? Platform,
    string? Title,
    string? Url,
    string? Summary,
    string? ImageUrl,
    DateTimeOffset OccurredAt);

/// <summary>The activity sections of the profile page. All but <see cref="Other"/> can also be filtered on the platform.</summary>
public static class PlatformActivityGroups
{
    public const string Blog = "blog";
    public const string Forum = "forum";
    public const string Packages = "packages";
    public const string Meetups = "meetups";
    public const string Videos = "videos";
    public const string Social = "social";

    /// <summary>Activity a member added by hand that has no section of its own.</summary>
    public const string Other = "other";

    public static readonly IReadOnlyList<string> FromPlatform = [Blog, Forum, Packages, Meetups, Videos, Social];

    public static readonly IReadOnlyList<string> All = [.. FromPlatform, Other];
}

namespace UmbracoCommunity.Web.Features.Profiles.Models;

/// <summary>A member's profile on the external content platform.</summary>
public sealed record PlatformProfile(string Id, IReadOnlyList<int> MvpYears);

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

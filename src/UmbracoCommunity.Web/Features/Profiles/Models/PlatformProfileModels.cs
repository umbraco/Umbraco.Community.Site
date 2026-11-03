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

/// <summary>The activity groups the platform can filter on that the profile page has a section for.</summary>
public static class PlatformActivityGroups
{
    public const string Blog = "blog";
    public const string Forum = "forum";
    public const string Packages = "packages";
    public const string Meetups = "meetups";
    public const string Videos = "videos";
    public const string Social = "social";

    public static readonly IReadOnlyList<string> All = [Blog, Forum, Packages, Meetups, Videos, Social];
}

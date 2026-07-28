namespace UmbracoCommunity.Web.Features.Feeds.CommunityBlogs.Models;

/// <summary>Incoming request body for both the preview and submit feed-submission endpoints.</summary>
public sealed record FeedSubmissionRequest
{
    public string? FeedUrl { get; init; }

    public string? Name { get; init; }

    public string? GithubUsername { get; init; }

    /// <summary>Anti-spam honeypot. A non-empty value means a bot filled a hidden field.</summary>
    public string? Honeypot { get; init; }
}

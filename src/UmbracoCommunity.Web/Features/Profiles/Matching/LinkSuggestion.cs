namespace UmbracoCommunity.Web.Features.Profiles.Matching;

/// <summary>
/// An account or feed the external content platform ties to the member, which they have not added to their links.
/// </summary>
/// <param name="Platform">The platform an account is on (e.g. "mastodon"); null for a feed or when not known.</param>
/// <param name="Name">The account's username or the feed's name, when known.</param>
/// <param name="Kind">"Account" or "Feed", see <see cref="LinkSuggestionKinds"/>.</param>
public sealed record LinkSuggestion(string Url, string? Platform, string? Name, string Kind);

public static class LinkSuggestionKinds
{
    public const string Account = "Account";
    public const string Feed = "Feed";
}

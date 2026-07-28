namespace UmbracoCommunity.BlogAnnouncements.Delivery;

/// <summary>Typed HttpClient wrapper for posting to the Discord webhook.</summary>
public sealed class DiscordAnnouncerHttpClient
{
    public DiscordAnnouncerHttpClient(HttpClient client) => Client = client;

    public HttpClient Client { get; }
}

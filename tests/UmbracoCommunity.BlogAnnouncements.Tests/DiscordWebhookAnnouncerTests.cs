using System.Text.Json;
using FluentAssertions;
using UmbracoCommunity.BlogAnnouncements.Delivery;
using Xunit;

namespace UmbracoCommunity.BlogAnnouncements.Tests;

public class DiscordWebhookAnnouncerTests
{
    private static JsonElement BuildJson(AnnouncementPayload payload, string? publicBaseUrl = null)
    {
        var body = DiscordWebhookAnnouncer.BuildBody(payload, publicBaseUrl);
        var json = JsonSerializer.Serialize(body, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return JsonDocument.Parse(json).RootElement;
    }

    private static AnnouncementPayload Payload(string? avatar = "https://cdn.example/a.png", string? cover = "https://cdn.example/c.png")
        => new(
            Title: "Hello World",
            Url: "https://blog.example/hello",
            Excerpt: "An excerpt",
            AuthorName: "Jane Dev",
            AuthorProfileUrl: "https://profile.example/jane",
            AvatarUrl: avatar,
            CoverImageUrl: cover,
            PublishedAt: new DateTimeOffset(2026, 6, 11, 11, 45, 0, TimeSpan.Zero));

    [Fact]
    public void BuildBody_MapsCoreEmbedFields()
    {
        var root = BuildJson(Payload());

        var embed = root.GetProperty("embeds")[0];
        embed.GetProperty("title").GetString().Should().Be("Hello World");
        embed.GetProperty("url").GetString().Should().Be("https://blog.example/hello");
        embed.GetProperty("description").GetString().Should().Be("An excerpt");
        embed.GetProperty("image").GetProperty("url").GetString().Should().Be("https://cdn.example/c.png");
        embed.GetProperty("author").GetProperty("name").GetString().Should().Be("Jane Dev");
        embed.GetProperty("author").GetProperty("url").GetString().Should().Be("https://profile.example/jane");
        embed.GetProperty("author").GetProperty("icon_url").GetString().Should().Be("https://cdn.example/a.png");
        root.GetProperty("username").GetString().Should().Be("Community Blog Posts");
        // Message identity is constant: no per-message avatar — the webhook's configured avatar shows.
        root.TryGetProperty("avatar_url", out _).Should().BeFalse();
    }

    [Fact]
    public void BuildBody_AlwaysSetsPerMessageUsername_EvenWithoutUsableAvatar()
    {
        var root = BuildJson(Payload(avatar: null));

        root.GetProperty("username").GetString().Should().Be("Community Blog Posts");
        root.TryGetProperty("avatar_url", out _).Should().BeFalse();
        root.GetProperty("embeds")[0].GetProperty("author").TryGetProperty("icon_url", out _).Should().BeFalse();
    }

    [Fact]
    public void BuildBody_TimestampIsPublishedAt()
    {
        var root = BuildJson(Payload());
        var timestamp = root.GetProperty("embeds")[0].GetProperty("timestamp").GetString();
        DateTimeOffset.Parse(timestamp!).Should().Be(new DateTimeOffset(2026, 6, 11, 11, 45, 0, TimeSpan.Zero));
    }

    [Fact]
    public void BuildBody_DropsSvgAvatarFromEmbedAuthorIcon()
    {
        var root = BuildJson(Payload(avatar: "https://cdn.example/avatar.svg"));
        root.GetProperty("embeds")[0].GetProperty("author").TryGetProperty("icon_url", out _).Should().BeFalse();
    }

    [Fact]
    public void BuildBody_DropsSvgAvatarEvenWithQueryString()
    {
        var root = BuildJson(Payload(avatar: "https://cdn.example/avatar.svg?token=abc"));
        root.GetProperty("embeds")[0].GetProperty("author").TryGetProperty("icon_url", out _).Should().BeFalse();
    }

    [Fact]
    public void BuildBody_OmitsImageWhenNoCover()
    {
        var root = BuildJson(Payload(cover: null));
        root.GetProperty("embeds")[0].TryGetProperty("image", out _).Should().BeFalse();
    }

    [Fact]
    public void BuildBody_ResolvesRootRelativeCoverAgainstPublicBaseUrl()
    {
        var root = BuildJson(
            Payload(cover: "/community-blog-images/abc123.png"),
            publicBaseUrl: "https://community.umbraco.com");

        root.GetProperty("embeds")[0].GetProperty("image").GetProperty("url").GetString()
            .Should().Be("https://community.umbraco.com/community-blog-images/abc123.png");
    }

    [Fact]
    public void BuildBody_ResolvesRootRelativeAvatarAgainstPublicBaseUrl()
    {
        var root = BuildJson(
            Payload(avatar: "/community-blog-images/avatar456.jpg"),
            publicBaseUrl: "https://community.umbraco.com");

        root.GetProperty("embeds")[0].GetProperty("author").GetProperty("icon_url").GetString()
            .Should().Be("https://community.umbraco.com/community-blog-images/avatar456.jpg");
    }

    [Fact]
    public void BuildBody_DropsRootRelativeCoverWhenNoPublicBaseUrlConfigured()
    {
        var root = BuildJson(Payload(cover: "/community-blog-images/abc123.png"), publicBaseUrl: null);
        root.GetProperty("embeds")[0].TryGetProperty("image", out _).Should().BeFalse();
    }

    [Fact]
    public void BuildBody_DropsRootRelativeAvatarWhenNoPublicBaseUrlConfigured()
    {
        var root = BuildJson(Payload(avatar: "/community-blog-images/avatar456.jpg"), publicBaseUrl: null);
        root.GetProperty("embeds")[0].GetProperty("author").TryGetProperty("icon_url", out _).Should().BeFalse();
    }
}

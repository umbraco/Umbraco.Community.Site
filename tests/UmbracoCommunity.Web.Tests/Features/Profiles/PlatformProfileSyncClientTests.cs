using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UmbracoCommunity.Web.Features.Feeds.CommunityBlogs;
using UmbracoCommunity.Web.Features.Profiles;
using UmbracoCommunity.Web.Features.Profiles.Models;
using UmbracoCommunity.Web.Tests.Features.Feeds.Calendar;
using Xunit;

namespace UmbracoCommunity.Web.Tests.Features.Profiles;

public class PlatformProfileSyncClientTests
{
    private static PlatformProfileSyncClient CreateClient(StubHandler handler, string apiKey = "key", bool withBaseAddress = true)
    {
        var http = new HttpClient(handler);
        if (withBaseAddress)
        {
            http.BaseAddress = new Uri("https://platform.example/v1/");
        }

        var options = new TestOptions(new CommunityBlogsOptions { ApiKey = apiKey });
        return new PlatformProfileSyncClient(
            new CommunityBlogsApiClient(new CommunityBlogsHttpClient(http), options),
            NullLogger<PlatformProfileSyncClient>.Instance);
    }

    [Fact]
    public async Task Looks_the_profile_up_by_github_handle_with_the_bare_key()
    {
        HttpRequestMessage? sent = null;
        var handler = new StubHandler(request =>
        {
            sent = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{ "data": { "id": "p1", "identities": [], "feeds": [], "mvpYears": [2020, 2021] } }"""),
            };
        });

        var profile = await CreateClient(handler).FindProfileAsync("octo cat");

        sent!.RequestUri!.AbsoluteUri.Should().Be("https://platform.example/v1/profiles/lookup?github=octo%20cat");
        sent.Headers.GetValues("Authorization").Should().Equal("key");
        profile.Should().NotBeNull();
        profile!.Id.Should().Be("p1");
        profile.MvpYears.Should().Equal(2020, 2021);
    }

    [Fact]
    public async Task Reads_the_identities_and_feeds_the_platform_ties_to_the_profile()
    {
        var handler = StubHandler.Json("""
            { "data": { "id": "p1",
                        "identities": [ { "platform": "mastodon", "username": "octocat", "profileUrl": "https://social.example/@octocat" },
                                        { "platform": "broken", "username": "x", "profileUrl": null } ],
                        "feeds": [ { "url": "https://octo.example/rss", "name": "Octo's blog" }, { "url": " ", "name": "Empty" } ],
                        "mvpYears": [] } }
            """);

        var profile = await CreateClient(handler).FindProfileAsync("octocat");

        profile!.Identities.Should().Equal(new PlatformIdentity("mastodon", "octocat", "https://social.example/@octocat"));
        profile.Feeds.Should().Equal(new PlatformFeed("https://octo.example/rss", "Octo's blog"));
    }

    [Fact]
    public async Task A_lookup_without_identities_or_feeds_has_none()
    {
        var profile = await CreateClient(StubHandler.Json("""{ "data": { "id": "p1" } }""")).FindProfileAsync("octocat");

        profile!.Identities.Should().BeEmpty();
        profile.Feeds.Should().BeEmpty();
        profile.MvpYears.Should().BeEmpty();
    }

    [Fact]
    public async Task Verifies_links_with_a_post_carrying_the_handle_and_the_links()
    {
        HttpRequestMessage? sent = null;
        string? body = null;
        var handler = new StubHandler(request =>
        {
            sent = request;
            body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    { "data": [ { "url": "https://social.example/@octocat", "status": "confirmed", "platform": "mastodon" },
                                { "url": "https://octo.example", "status": "Unknown" },
                                { "url": null, "status": "confirmed" } ] }
                    """),
            };
        });

        var checks = await CreateClient(handler).VerifyLinksAsync("octocat", ["https://social.example/@octocat", "https://octo.example", "http://insecure.example"]);

        sent!.Method.Should().Be(HttpMethod.Post);
        sent.RequestUri!.AbsoluteUri.Should().Be("https://platform.example/v1/profiles/verify-links");
        sent.Headers.GetValues("Authorization").Should().Equal("key");
        using var json = System.Text.Json.JsonDocument.Parse(body!);
        json.RootElement.GetProperty("github").GetString().Should().Be("octocat");
        json.RootElement.GetProperty("links").EnumerateArray().Select(l => l.GetString())
            .Should().Equal("https://social.example/@octocat", "https://octo.example");
        checks.Should().Equal(
            new PlatformLinkCheck("https://social.example/@octocat", PlatformLinkStatuses.Confirmed, "mastodon"),
            new PlatformLinkCheck("https://octo.example", PlatformLinkStatuses.Unknown, null));
    }

    [Fact]
    public async Task Verifies_at_most_twenty_links_per_request()
    {
        var sizes = new List<int>();
        var handler = new StubHandler(request =>
        {
            using var json = System.Text.Json.JsonDocument.Parse(request.Content!.ReadAsStringAsync().GetAwaiter().GetResult());
            sizes.Add(json.RootElement.GetProperty("links").GetArrayLength());
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{ "data": [] }""") };
        });
        var links = Enumerable.Range(1, 45).Select(i => $"https://octo.example/{i}").ToList();

        await CreateClient(handler).VerifyLinksAsync("octocat", links);

        sizes.Should().Equal(20, 20, 5);
    }

    [Fact]
    public async Task A_failed_link_check_is_thrown()
    {
        var act = () => CreateClient(StubHandler.Status(HttpStatusCode.BadRequest)).VerifyLinksAsync("octocat", ["https://octo.example"]);

        await act.Should().ThrowAsync<CommunityBlogsApiException>();
    }

    [Fact]
    public async Task No_match_is_null_not_an_error()
    {
        var profile = await CreateClient(StubHandler.Status(HttpStatusCode.NotFound)).FindProfileAsync("octocat");

        profile.Should().BeNull();
    }

    [Fact]
    public async Task Reads_one_group_of_activities()
    {
        Uri? requested = null;
        var handler = new StubHandler(request =>
        {
            requested = request.RequestUri;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    { "data": [ { "id": "a1", "type": "blog:post", "platform": "web", "title": "Hello",
                                  "url": "https://example.dev/hello", "summary": null, "imageUrl": null,
                                  "occurredAt": "2026-06-01T12:34:56.000Z" } ],
                      "pagination": { "nextCursor": null, "hasMore": false } }
                    """),
            };
        });

        var activities = await CreateClient(handler).GetActivitiesAsync("p1", "blog", 5);

        requested!.ToString().Should().Be("https://platform.example/v1/profiles/p1/activities?group=blog&limit=5");
        activities.Should().ContainSingle().Which.Title.Should().Be("Hello");
    }

    [Fact]
    public async Task A_server_error_is_thrown_so_the_caller_can_cache_the_failure()
    {
        var act = () => CreateClient(StubHandler.Status(HttpStatusCode.InternalServerError)).GetActivitiesAsync("p1", "blog", 5);

        await act.Should().ThrowAsync<CommunityBlogsApiException>();
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("key", false)]
    public async Task Without_an_address_and_key_no_request_is_made(string apiKey, bool withBaseAddress)
    {
        var handler = StubHandler.Status(HttpStatusCode.OK);
        var client = CreateClient(handler, apiKey, withBaseAddress);

        (await client.FindProfileAsync("octocat")).Should().BeNull();
        (await client.GetActivitiesAsync("p1", "blog", 5)).Should().BeEmpty();
        (await client.VerifyLinksAsync("octocat", ["https://octo.example"])).Should().BeEmpty();
        handler.CallCount.Should().Be(0);
    }

    private sealed class TestOptions(CommunityBlogsOptions value) : IOptionsMonitor<CommunityBlogsOptions>
    {
        public CommunityBlogsOptions CurrentValue => value;
        public CommunityBlogsOptions Get(string? name) => value;
        public IDisposable? OnChange(Action<CommunityBlogsOptions, string?> listener) => null;
    }
}

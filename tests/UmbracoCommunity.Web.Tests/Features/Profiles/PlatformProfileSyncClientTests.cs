using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UmbracoCommunity.Web.Features.Feeds.CommunityBlogs;
using UmbracoCommunity.Web.Features.Profiles;
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
        handler.CallCount.Should().Be(0);
    }

    private sealed class TestOptions(CommunityBlogsOptions value) : IOptionsMonitor<CommunityBlogsOptions>
    {
        public CommunityBlogsOptions CurrentValue => value;
        public CommunityBlogsOptions Get(string? name) => value;
        public IDisposable? OnChange(Action<CommunityBlogsOptions, string?> listener) => null;
    }
}

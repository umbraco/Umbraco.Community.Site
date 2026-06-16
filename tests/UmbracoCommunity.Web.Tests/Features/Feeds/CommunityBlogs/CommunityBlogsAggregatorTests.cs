using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using UmbracoCommunity.Web.Features.Feeds.CommunityBlogs;
using Xunit;

namespace UmbracoCommunity.Web.Tests.Features.Feeds.CommunityBlogs;

public class CommunityBlogsAggregatorTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-06-15T10:00:00Z");

    private static string Page(string id, string publishedAt, string? nextCursor, bool hasMore) => $$"""
    {
      "data": [
        {
          "id": "{{id}}",
          "type": "blog_post",
          "title": "Post {{id}}",
          "url": "https://blog.example/{{id}}",
          "content": "Excerpt {{id}}",
          "coverImageUrl": "https://blog.example/{{id}}.png",
          "publishedAt": "{{publishedAt}}",
          "author": { "name": "Author {{id}}", "profileUrl": "https://blog.example", "avatarUrl": "https://blog.example/avatar.png" }
        }
      ],
      "pagination": { "nextCursor": {{(nextCursor is null ? "null" : $"\"{nextCursor}\"")}}, "hasMore": {{(hasMore ? "true" : "false")}} }
    }
    """;

    private static (SphereApiClient Client, StubHandler Handler) CreateClient(
        StubHandler handler, CommunityBlogsOptions options)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://test.local/api/v1/") };
        var typed = new SphereHttpClient(http);
        var client = new SphereApiClient(typed, new TestOptionsMonitor<CommunityBlogsOptions>(options));
        return (client, handler);
    }

    [Fact]
    public async Task Client_sends_authorization_header_and_cursor()
    {
        var handler = StubHandler.Json(Page("a", "2026-06-10T00:00:00Z", null, false));
        var (client, h) = CreateClient(handler, new CommunityBlogsOptions { ApiKey = "psk_test" });

        await client.GetBlogPostsAsync("CUR123", 50, CancellationToken.None);

        var request = h.Requests.Single();
        request.Headers.GetValues("Authorization").Single().Should().Be("psk_test");
        request.RequestUri!.Query.Should().Contain("limit=50").And.Contain("cursor=CUR123");
    }

    private static CommunityBlogsAggregator CreateAggregator(StubHandler handler, CommunityBlogsOptions options)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://test.local/api/v1/") };
        var client = new SphereApiClient(new SphereHttpClient(http), new TestOptionsMonitor<CommunityBlogsOptions>(options));
        return new CommunityBlogsAggregator(
            client,
            new TestOptionsMonitor<CommunityBlogsOptions>(options),
            new FixedTimeProvider(Now),
            NullLogger<CommunityBlogsAggregator>.Instance);
    }

    [Fact]
    public async Task Maps_fields_and_orders_newest_first()
    {
        // page 1 -> hasMore -> page 2 (end). Older first in the wire order to prove re-sort.
        var responder = new Func<HttpRequestMessage, HttpResponseMessage>(req =>
        {
            var body = req.RequestUri!.Query.Contains("cursor=NEXT")
                ? Page("new", "2026-06-12T00:00:00Z", null, false)
                : Page("old", "2026-06-01T00:00:00Z", "NEXT", true);
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };
        });
        var aggregator = CreateAggregator(new StubHandler(responder),
            new CommunityBlogsOptions { ApiKey = "psk_test", FetchBatchSize = 1, MaxPosts = 100 });

        var data = await aggregator.BuildAsync(CancellationToken.None);

        data.Should().NotBeNull();
        data!.Posts.Should().HaveCount(2);
        data.Posts[0].Id.Should().Be("new"); // newest first
        data.Posts[0].Title.Should().Be("Post new");
        data.Posts[0].Url.Should().Be("https://blog.example/new");
        data.Posts[0].Excerpt.Should().Be("Excerpt new");
        data.Posts[0].CoverImageUrl.Should().Be("https://blog.example/new.png");
        data.Posts[0].AuthorName.Should().Be("Author new");
        data.Posts[0].AuthorAvatarUrl.Should().Be("https://blog.example/avatar.png");
        data.LastUpdatedUtc.Should().Be(Now);
    }

    [Fact]
    public async Task Stops_at_MaxPosts()
    {
        // Every page says hasMore with a unique cursor, so only the cap halts the walk.
        var counter = 0;
        var responder = new Func<HttpRequestMessage, HttpResponseMessage>(_ =>
        {
            counter++;
            var body = Page($"p{counter}", "2026-06-01T00:00:00Z", $"C{counter}", true);
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };
        });
        var aggregator = CreateAggregator(new StubHandler(responder),
            new CommunityBlogsOptions { ApiKey = "psk_test", FetchBatchSize = 1, MaxPosts = 3 });

        var data = await aggregator.BuildAsync(CancellationToken.None);

        data!.Posts.Should().HaveCount(3);
    }

    [Fact]
    public async Task Returns_null_when_first_page_fails()
    {
        var aggregator = CreateAggregator(StubHandler.Throws(),
            new CommunityBlogsOptions { ApiKey = "psk_test" });

        var data = await aggregator.BuildAsync(CancellationToken.None);

        data.Should().BeNull(); // signals "keep existing data"
    }

    [Fact]
    public async Task Returns_null_when_api_key_missing()
    {
        var aggregator = CreateAggregator(StubHandler.Json(Page("a", "2026-06-01T00:00:00Z", null, false)),
            new CommunityBlogsOptions { ApiKey = "" });

        var data = await aggregator.BuildAsync(CancellationToken.None);

        data.Should().BeNull();
    }

    [Fact]
    public async Task Skips_items_without_url_or_title()
    {
        const string body = """
        {
          "data": [
            { "id": "1", "type": "blog_post", "title": "Good", "url": "https://x/1", "content": null, "coverImageUrl": null, "publishedAt": "2026-06-01T00:00:00Z", "author": null },
            { "id": "2", "type": "blog_post", "title": "No URL", "url": null, "content": null, "coverImageUrl": null, "publishedAt": "2026-06-02T00:00:00Z", "author": null }
          ],
          "pagination": { "nextCursor": null, "hasMore": false }
        }
        """;
        var aggregator = CreateAggregator(StubHandler.Json(body),
            new CommunityBlogsOptions { ApiKey = "psk_test" });

        var data = await aggregator.BuildAsync(CancellationToken.None);

        data!.Posts.Should().ContainSingle().Which.Id.Should().Be("1");
        data.Posts[0].Excerpt.Should().BeNull();
        data.Posts[0].AuthorName.Should().BeNull();
    }

    [Fact]
    public async Task Returns_partial_data_when_a_later_page_fails()
    {
        // First page succeeds (hasMore -> cursor NEXT); the second page throws.
        var responder = new Func<HttpRequestMessage, HttpResponseMessage>(req =>
        {
            if (req.RequestUri!.Query.Contains("cursor=NEXT"))
            {
                throw new HttpRequestException("boom on page 2");
            }

            var body = Page("first", "2026-06-01T00:00:00Z", "NEXT", true);
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };
        });
        var aggregator = CreateAggregator(new StubHandler(responder),
            new CommunityBlogsOptions { ApiKey = "psk_test", FetchBatchSize = 1, MaxPosts = 100 });

        var data = await aggregator.BuildAsync(CancellationToken.None);

        data.Should().NotBeNull(); // first page succeeded, so we keep what we got
        data!.Posts.Should().ContainSingle().Which.Id.Should().Be("first");
    }

    [Fact]
    public async Task Stops_when_cursor_repeats()
    {
        // Every page returns the SAME nextCursor, so the repeated-cursor guard must halt the walk.
        var counter = 0;
        var responder = new Func<HttpRequestMessage, HttpResponseMessage>(_ =>
        {
            counter++;
            var body = Page($"p{counter}", "2026-06-01T00:00:00Z", "SAME", true);
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };
        });
        var handler = new StubHandler(responder);
        var aggregator = CreateAggregator(handler,
            new CommunityBlogsOptions { ApiKey = "psk_test", FetchBatchSize = 1, MaxPosts = 100 });

        var data = await aggregator.BuildAsync(CancellationToken.None);

        data!.Posts.Should().HaveCount(2);   // page 1 + page 2, then the repeated cursor stops it
        handler.CallCount.Should().Be(2);
    }

    [Fact]
    public async Task Decodes_html_entities_in_text_fields()
    {
        const string body = """
        {
          "data": [
            {
              "id": "1",
              "type": "blog_post",
              "title": "Tom &amp; Jerry",
              "url": "https://x/1",
              "content": "Here&#x27;s how I did it &amp; more",
              "coverImageUrl": null,
              "publishedAt": "2026-06-01T00:00:00Z",
              "author": { "name": "Joe &#x27;Dev&#x27;", "profileUrl": null, "avatarUrl": null }
            }
          ],
          "pagination": { "nextCursor": null, "hasMore": false }
        }
        """;
        var aggregator = CreateAggregator(StubHandler.Json(body),
            new CommunityBlogsOptions { ApiKey = "psk_test" });

        var data = await aggregator.BuildAsync(CancellationToken.None);

        var post = data!.Posts.Single();
        post.Title.Should().Be("Tom & Jerry");
        post.Excerpt.Should().Be("Here's how I did it & more");
        post.AuthorName.Should().Be("Joe 'Dev'");
    }
}

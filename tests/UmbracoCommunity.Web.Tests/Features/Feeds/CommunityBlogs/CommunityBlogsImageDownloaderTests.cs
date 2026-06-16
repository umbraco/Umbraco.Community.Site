using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using UmbracoCommunity.Web.Features.Feeds.CommunityBlogs;
using Xunit;

namespace UmbracoCommunity.Web.Tests.Features.Feeds.CommunityBlogs;

public class CommunityBlogsImageDownloaderTests : IDisposable
{
    private readonly string _webRoot = Path.Combine(Path.GetTempPath(), "cb-img-" + Guid.NewGuid().ToString("N"));

    private CommunityBlogsImageDownloader Create(StubHandler handler)
    {
        Directory.CreateDirectory(_webRoot);
        var env = new Mock<IWebHostEnvironment>();
        env.SetupGet(e => e.WebRootPath).Returns(_webRoot);
        var http = new HttpClient(handler);
        return new CommunityBlogsImageDownloader(
            new CommunityBlogsImageHttpClient(http), env.Object, NullLogger<CommunityBlogsImageDownloader>.Instance);
    }

    private static StubHandler ImageResponder(string contentType, byte[] bytes) => new(_ =>
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
    });

    private static CommunityBlogsData DataWith(params CommunityBlogPost[] posts) =>
        new(posts, DateTimeOffset.Parse("2026-06-15T10:00:00Z"));

    private static CommunityBlogPost Post(string id, string? cover, string? avatar) =>
        new(id, "Title " + id, "https://blog.example/" + id, null, cover, DateTimeOffset.Parse("2026-06-01T00:00:00Z"), "Author", avatar, null);

    [Fact]
    public async Task Downloads_cover_and_avatar_to_local_paths()
    {
        var d = Create(ImageResponder("image/png", new byte[] { 1, 2, 3 }));
        var result = await d.LocalizeAsync(DataWith(Post("1", "https://x/cover.png", "https://x/avatar.png")), CancellationToken.None);

        var post = result.Posts.Single();
        post.CoverImageUrl.Should().StartWith("/community-blog-images/").And.EndWith(".png");
        post.AuthorAvatarUrl.Should().StartWith("/community-blog-images/").And.EndWith(".png");
        Directory.GetFiles(Path.Combine(_webRoot, "community-blog-images")).Should().HaveCount(2);
    }

    [Fact]
    public async Task Non_image_content_type_becomes_null()
    {
        var d = Create(new StubHandler(_ =>
        {
            var c = new StringContent("<html>", System.Text.Encoding.UTF8, "text/html");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = c };
        }));
        var result = await d.LocalizeAsync(DataWith(Post("1", "https://x/notimage", null)), CancellationToken.None);
        result.Posts.Single().CoverImageUrl.Should().BeNull();
    }

    [Fact]
    public async Task Download_failure_becomes_null_and_does_not_throw()
    {
        var d = Create(StubHandler.Throws());
        var result = await d.LocalizeAsync(DataWith(Post("1", "https://x/cover.png", null)), CancellationToken.None);
        result.Posts.Single().CoverImageUrl.Should().BeNull();
    }

    [Fact]
    public async Task Null_image_urls_stay_null_without_http()
    {
        var handler = ImageResponder("image/png", new byte[] { 1 });
        var d = Create(handler);
        var result = await d.LocalizeAsync(DataWith(Post("1", null, null)), CancellationToken.None);
        result.Posts.Single().CoverImageUrl.Should().BeNull();
        handler.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Same_url_is_downloaded_once_and_reused()
    {
        var handler = ImageResponder("image/png", new byte[] { 9 });
        var d = Create(handler);
        var result = await d.LocalizeAsync(
            DataWith(Post("1", "https://x/same.png", null), Post("2", "https://x/same.png", null)),
            CancellationToken.None);

        result.Posts[0].CoverImageUrl.Should().Be(result.Posts[1].CoverImageUrl);
        handler.CallCount.Should().Be(1); // second post reused the existing file
        Directory.GetFiles(Path.Combine(_webRoot, "community-blog-images")).Should().HaveCount(1);
    }

    [Fact]
    public async Task Prunes_files_no_longer_referenced()
    {
        var dir = Path.Combine(_webRoot, "community-blog-images");
        Directory.CreateDirectory(dir);
        var stale = Path.Combine(dir, "stale.png");
        File.WriteAllBytes(stale, new byte[] { 0 });

        var d = Create(ImageResponder("image/png", new byte[] { 1, 2 }));
        await d.LocalizeAsync(DataWith(Post("1", "https://x/cover.png", null)), CancellationToken.None);

        File.Exists(stale).Should().BeFalse(); // pruned
        Directory.GetFiles(dir).Should().ContainSingle().Which.Should().EndWith(".png");
    }

    [Fact]
    public async Task Already_local_path_is_kept_and_not_pruned()
    {
        var dir = Path.Combine(_webRoot, "community-blog-images");
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, "keep.png"), new byte[] { 1 });

        var handler = ImageResponder("image/png", new byte[] { 1 });
        var d = Create(handler);
        var result = await d.LocalizeAsync(DataWith(Post("1", "/community-blog-images/keep.png", null)), CancellationToken.None);

        result.Posts.Single().CoverImageUrl.Should().Be("/community-blog-images/keep.png");
        handler.CallCount.Should().Be(0);
        File.Exists(Path.Combine(dir, "keep.png")).Should().BeTrue();
    }

    public void Dispose()
    {
        if (Directory.Exists(_webRoot)) Directory.Delete(_webRoot, recursive: true);
    }
}

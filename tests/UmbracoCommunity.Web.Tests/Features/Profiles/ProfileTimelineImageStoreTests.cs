using System.Net;
using System.Net.Http.Headers;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Umbraco.Cms.Core.Cache;
using UmbracoCommunity.Web.Features.Profiles;
using UmbracoCommunity.Web.Features.Profiles.LinkPreviews;
using Xunit;

namespace UmbracoCommunity.Web.Tests.Features.Profiles;

public class ProfileTimelineImageStoreTests
{
    // Address literals need no DNS lookup, so these tests never touch the network.
    private const string ImageUrl = "https://93.184.216.34/cover.png";

    internal static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4];

    private readonly InMemoryMediaFileSystem _media = new();

    private static AppCaches NewCaches() => new(new ObjectCacheAppCache(), NoAppCache.Instance, new IsolatedCaches(_ => NoAppCache.Instance));

    private (ProfileTimelineImageStore Store, StubHandler Handler) Create(Func<HttpRequestMessage, HttpResponseMessage> respond, AppCaches? caches = null)
    {
        var handler = new StubHandler(respond);
        var store = new ProfileTimelineImageStore(
            _media.FileSystem,
            new LinkPreviewHttpClient(new HttpClient(handler)),
            caches ?? NewCaches(),
            NullLogger<ProfileTimelineImageStore>.Instance);
        return (store, handler);
    }

    internal static HttpResponseMessage Image(byte[] bytes, string mediaType = "image/png") =>
        new(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) { Headers = { ContentType = new MediaTypeHeaderValue(mediaType) } } };

    [Fact]
    public async Task A_picture_is_stored_in_the_media_file_system_named_by_a_hash_of_its_address()
    {
        var (store, _) = Create(_ => Image(Png));

        var url = await store.GetOrStoreAsync(ImageUrl, CancellationToken.None);

        var path = $"{ProfileTimelineImageStore.Folder}/{ProfileTimelineImageStore.FileName(ImageUrl)}.png";
        url.Should().Be("/media/" + path);
        _media.Files.Should().ContainKey(path).WhoseValue.Should().Equal(Png);
        ProfileTimelineImageStore.FileName(ImageUrl).Should().MatchRegex("^[0-9a-f]{32}$");
    }

    [Fact]
    public async Task A_picture_is_stored_once_and_reused()
    {
        var (store, handler) = Create(_ => Image(Png));

        var first = await store.GetOrStoreAsync(ImageUrl, CancellationToken.None);
        var second = await store.GetOrStoreAsync(ImageUrl, CancellationToken.None);

        second.Should().Be(first);
        handler.Requests.Should().HaveCount(1);
        _media.Files.Should().HaveCount(1);
    }

    [Fact]
    public async Task The_stored_copy_keeps_being_used_after_the_picture_disappears_from_its_site()
    {
        var (store, _) = Create(_ => Image(Png));
        var stored = await store.GetOrStoreAsync(ImageUrl, CancellationToken.None);

        // Another server (fresh memory), with the picture's site now failing.
        var (later, handler) = Create(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        later.FindStored(ImageUrl).Should().Be(stored);
        (await later.GetOrStoreAsync(ImageUrl, CancellationToken.None)).Should().Be(stored);
        handler.Requests.Should().BeEmpty();
    }

    [Theory]
    [InlineData("text/html")]
    [InlineData("image/svg+xml")]
    [InlineData("application/octet-stream")]
    public async Task A_response_that_is_not_an_accepted_image_type_is_refused(string mediaType)
    {
        var (store, _) = Create(_ => Image(Png, mediaType));

        (await store.GetOrStoreAsync(ImageUrl, CancellationToken.None)).Should().BeNull();
        _media.Files.Should().BeEmpty();
    }

    [Fact]
    public async Task A_file_that_claims_to_be_an_image_but_is_not_is_refused()
    {
        var (store, _) = Create(_ => Image(Encoding.UTF8.GetBytes("<svg onload=alert(1)></svg>")));

        (await store.GetOrStoreAsync(ImageUrl, CancellationToken.None)).Should().BeNull();
        _media.Files.Should().BeEmpty();
    }

    [Fact]
    public async Task A_picture_over_the_size_limit_is_refused()
    {
        var big = new byte[ProfileTimelineImageStore.MaxBytes + 1];
        Png.CopyTo(big, 0);
        var (store, _) = Create(_ => Image(big));

        (await store.GetOrStoreAsync(ImageUrl, CancellationToken.None)).Should().BeNull();
        _media.Files.Should().BeEmpty();
    }

    [Theory]
    [InlineData("http://93.184.216.34/cover.png")]
    [InlineData("https://10.0.0.5/cover.png")]
    [InlineData("https://127.0.0.1/cover.png")]
    public async Task Pictures_that_are_not_https_or_point_inside_are_never_fetched(string url)
    {
        var (store, handler) = Create(_ => Image(Png));

        (await store.GetOrStoreAsync(url, CancellationToken.None)).Should().BeNull();
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task A_redirect_to_a_blocked_address_is_not_followed()
    {
        var (store, handler) = Create(request => request.RequestUri!.Host == "93.184.216.34"
            ? new HttpResponseMessage(HttpStatusCode.Found) { Headers = { Location = new Uri("https://10.0.0.5/inside.png") } }
            : Image(Png));

        (await store.GetOrStoreAsync(ImageUrl, CancellationToken.None)).Should().BeNull();
        handler.Requests.Should().HaveCount(1);
    }

    [Fact]
    public async Task A_failed_picture_is_not_fetched_again_straight_away()
    {
        var (store, handler) = Create(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        await store.GetOrStoreAsync(ImageUrl, CancellationToken.None);
        await store.GetOrStoreAsync(ImageUrl, CancellationToken.None);

        handler.Requests.Should().HaveCount(1);
    }

    [Fact]
    public void Each_accepted_format_is_recognised_by_its_first_bytes()
    {
        ProfileTimelineImageStore.Sniff([0xFF, 0xD8, 0xFF, 0xE0])!.Value.Extension.Should().Be(".jpg");
        ProfileTimelineImageStore.Sniff(Png)!.Value.Extension.Should().Be(".png");
        ProfileTimelineImageStore.Sniff("GIF89a..."u8)!.Value.Extension.Should().Be(".gif");
        ProfileTimelineImageStore.Sniff("RIFF\0\0\0\0WEBPVP8 "u8)!.Value.Extension.Should().Be(".webp");
        ProfileTimelineImageStore.Sniff("<svg"u8).Should().BeNull();
        ProfileTimelineImageStore.Sniff([]).Should().BeNull();
    }

    internal sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        private readonly object _lock = new();

        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            lock (_lock)
            {
                Requests.Add(request.RequestUri!);
            }

            return Task.FromResult(respond(request));
        }
    }
}

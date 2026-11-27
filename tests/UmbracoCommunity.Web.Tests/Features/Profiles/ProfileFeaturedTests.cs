using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Umbraco.Cms.Core.Cache;
using UmbracoCommunity.Web.Features.Profiles;
using UmbracoCommunity.Web.Features.Profiles.Data;
using UmbracoCommunity.Web.Features.Profiles.LinkPreviews;
using UmbracoCommunity.Web.Features.Profiles.Models;
using Xunit;
using StubHandler = UmbracoCommunity.Web.Tests.Features.Profiles.ProfileTimelineImageStoreTests.StubHandler;

namespace UmbracoCommunity.Web.Tests.Features.Profiles;

public class ProfileFeaturedTests : IDisposable
{
    // Address literals need no DNS lookup, so these tests never touch the network.
    private const string Host = "https://93.184.216.34";
    private const string OwnPicture = "/media/profile-activity/0123456789abcdef0123456789abcdef.png";

    private static readonly DateTimeOffset Now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly MemberProfilesTestDatabase _database = new();
    private readonly InMemoryMediaFileSystem _media = new();

    public ProfileFeaturedTests()
    {
        using var context = _database.CreateDbContext();
        context.Database.Migrate();
    }

    public void Dispose() => _database.Dispose();

    private (ProfileTimelinePreviewService Service, StubHandler Handler) Create(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var handler = new StubHandler(respond);
        var client = new LinkPreviewHttpClient(new HttpClient(handler));
        var caches = new AppCaches(new ObjectCacheAppCache(), NoAppCache.Instance, new IsolatedCaches(_ => NoAppCache.Instance));
        var service = new ProfileTimelinePreviewService(
            new LinkPreviewService(client, NullLogger<LinkPreviewService>.Instance),
            new ProfileTimelinePreviewStore(_database),
            new ProfileTimelineImageStore(_media.FileSystem, client, caches, NullLogger<ProfileTimelineImageStore>.Instance),
            caches,
            NullLogger<ProfileTimelinePreviewService>.Instance);
        return (service, handler);
    }

    private static HttpResponseMessage Html(string head) =>
        new(HttpStatusCode.OK) { Content = new StringContent($"<html><head>{head}</head></html>", Encoding.UTF8, "text/html") };

    // Pages under /with-picture/ offer a share picture; every other page offers none.
    private static HttpResponseMessage Site(HttpRequestMessage request)
    {
        var path = request.RequestUri!.AbsolutePath;
        if (path.EndsWith(".png", StringComparison.Ordinal))
        {
            return ProfileTimelineImageStoreTests.Image(ProfileTimelineImageStoreTests.Png);
        }

        return path.StartsWith("/with-picture/", StringComparison.Ordinal)
            ? Html("""<meta property="og:image" content="/share.png">""")
            : Html("<title>No picture here</title>");
    }

    private static CommunityProfile Profile(
        IReadOnlyList<ProfileBlogPost>? blogPosts = null,
        IReadOnlyList<ProfileVideo>? videos = null,
        IReadOnlyList<ProfilePackage>? packages = null,
        IReadOnlyList<ProfileTalk>? talks = null,
        IReadOnlyList<ProfileFeaturedItem>? featured = null,
        IReadOnlyDictionary<string, string>? activityImages = null) =>
        new(
            Slug: "octocat",
            PlatformId: null,
            Identity: new ProfileIdentity("The Octocat", null, null, null, null, null, false, [], "octocat", null, [], false),
            BlogPosts: blogPosts ?? [],
            Videos: videos ?? [],
            ForumPosts: [],
            Packages: packages ?? [],
            Talks: talks ?? [],
            Badges: [],
            SocialPosts: [],
            Meetups: [])
        {
            FeaturedItems = featured ?? [],
            ActivityImages = activityImages ?? new Dictionary<string, string>(),
        };

    private static ProfileFeaturedItem Pinned(string group, string id, string path) =>
        new(group, id, $"Title {id}", $"{Host}{path}", null, Now, null, "Why I pinned it");

    private async Task<IReadOnlyList<ProfileFeaturedHighlight>> ResolveAsync(ProfileTimelinePreviewService service, CommunityProfile profile)
    {
        var highlights = ProfileFeatured.Highlights(profile);
        var carded = await service.AddCardsAsync(ProfileFeatured.ImageLookups(highlights), CancellationToken.None);
        return ProfileFeatured.WithImages(highlights, carded);
    }

    [Fact]
    public async Task A_pinned_blog_post_whose_page_offers_a_picture_shows_our_stored_copy_of_it()
    {
        var (service, _) = Create(Site);

        var result = await ResolveAsync(service, Profile(featured: [Pinned(PlatformActivityGroups.Blog, "b1", "/with-picture/b1")]));

        var highlight = result.Single();
        highlight.Kind.Should().Be("Blog post");
        highlight.Reason.Should().Be("Why I pinned it");
        highlight.ImageUrl.Should().StartWith($"/media/{ProfileTimelineImageStore.Folder}/");
        _media.Files.Should().ContainSingle();
    }

    [Fact]
    public async Task A_pinned_item_whose_page_offers_no_picture_has_none()
    {
        var (service, _) = Create(Site);

        var result = await ResolveAsync(service, Profile(featured: [Pinned(PlatformActivityGroups.Blog, "b1", "/plain/b1")]));

        result.Single().ImageUrl.Should().BeNull();
    }

    [Fact]
    public async Task A_picture_the_member_attached_comes_first_and_nothing_is_fetched()
    {
        var (service, handler) = Create(Site);
        var profile = Profile(
            featured: [Pinned(PlatformActivityGroups.Other, "added:1", "/with-picture/a")],
            activityImages: new Dictionary<string, string> { ["added:1"] = OwnPicture });

        var result = await ResolveAsync(service, profile);

        result.Single().ImageUrl.Should().Be(OwnPicture);
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task A_pinned_video_uses_its_thumbnail_without_reading_its_page()
    {
        var (service, handler) = Create(Site);
        var profile = Profile(
            videos: [new ProfileVideo("A video", $"{Host}/plain/v1", $"{Host}/thumb.png", Now, ActivityId: "v1")],
            featured: [Pinned(PlatformActivityGroups.Videos, "v1", "/plain/v1")]);

        var result = await ResolveAsync(service, profile);

        result.Single().ImageUrl.Should().StartWith($"/media/{ProfileTimelineImageStore.Folder}/");
        result.Single().IsVideo.Should().BeTrue();
        handler.Requests.Should().ContainSingle().Which.AbsolutePath.Should().Be("/thumb.png");
    }

    [Fact]
    public async Task Without_pins_the_latest_of_each_type_is_shown_each_with_its_own_picture_or_none()
    {
        var (service, _) = Create(Site);
        var profile = Profile(
            packages: [new ProfilePackage("A package", $"{Host}/plain/p1", ["2.0.0"], "Does things", "p1", Now)],
            talks: [new ProfileTalk("A talk", "Codegarden", Now, null)],
            blogPosts: [new ProfileBlogPost("A post", $"{Host}/with-picture/b1", Now, "blog.example", null, "b1")],
            videos: [new ProfileVideo("A video", $"{Host}/plain/v1", null, Now, ActivityId: "v1")]);

        var result = await ResolveAsync(service, profile);

        result.Select(h => h.Kind).Should().Equal("Package", "Talk", "Blog post", "Video");
        result.Select(h => h.ImageUrl != null).Should().Equal(false, false, true, false);
        result[0].Chips.Should().Equal("2.0.0");
        result[1].ImageLookup.Should().BeNull("a talk without a page has nothing to take a picture from");
    }

    [Fact]
    public void No_highlight_has_a_picture_before_it_is_resolved()
    {
        var profile = Profile(
            packages: [new ProfilePackage("A package", $"{Host}/p", ["1.0.0"])],
            talks: [new ProfileTalk("A talk", "Codegarden", Now, $"{Host}/t")],
            blogPosts: [new ProfileBlogPost("A post", $"{Host}/b", Now, null)],
            videos: [new ProfileVideo("A video", $"{Host}/v", $"{Host}/thumb.png", Now)]);

        ProfileFeatured.Highlights(profile).Should().OnlyContain(h => h.ImageUrl == null);
    }

    [Theory]
    [InlineData("/media/profile-timeline/a.png", "https://example.com/a", true)]
    [InlineData(null, "https://example.com/a", false)]
    [InlineData("", "https://example.com/a", false)]
    [InlineData("/media/profile-timeline/a.png", null, false)]
    public void A_card_shows_its_picture_only_with_both_a_picture_and_a_link(string? imageUrl, string? url, bool expected)
    {
        var highlight = new ProfileFeaturedHighlight("Blog post", "blog", "A post", url) { ImageUrl = imageUrl };

        highlight.HasImage.Should().Be(expected);
    }

    [Fact]
    public void Highlights_without_a_card_keep_no_picture_and_the_rest_line_up_in_order()
    {
        var highlights = new[]
        {
            new ProfileFeaturedHighlight("Talk", "talk", "No page", null),
            new ProfileFeaturedHighlight("Blog post", "blog", "First", $"{Host}/a") { ImageLookup = Entry("a") },
            new ProfileFeaturedHighlight("Blog post", "blog", "Second", $"{Host}/b") { ImageLookup = Entry("b") },
        };
        var carded = new[]
        {
            Entry("a"),
            Entry("b") with { Card = new ProfileTimelineCard("/media/profile-timeline/b.png", "Second", null) },
        };

        var result = ProfileFeatured.WithImages(highlights, carded);

        result.Select(h => h.ImageUrl).Should().Equal(null, null, "/media/profile-timeline/b.png");
    }

    private static ProfileTimelineEntry Entry(string id) =>
        new(PlatformActivityGroups.Blog, id, id, $"{Host}/{id}", null, Now, null, null);
}

using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Security;
using UmbracoCommunity.Web.Features.Profiles;
using UmbracoCommunity.Web.Features.Profiles.Controllers;
using UmbracoCommunity.Web.Features.Profiles.Data;
using UmbracoCommunity.Web.Features.Profiles.LinkPreviews;
using UmbracoCommunity.Web.Features.Profiles.Models;
using Xunit;

namespace UmbracoCommunity.Web.Tests.Features.Profiles;

/// <summary>
/// Filling in the add-activity form from the page behind the link: what is read from a page, what the member gets back
/// (only what was found, cut to the entry limits, the picture as our own copy), and the guards on the server reading an
/// address a member typed (https and public addresses only, a per-member limit on page reads, answers cached per address).
/// </summary>
public class ProfileActivityLinkLookupTests : IDisposable
{
    // Address literals need no DNS lookup, so these tests never touch the network.
    private const string Host = "https://93.184.216.34";
    private static readonly DateTimeOffset Now = new(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);

    private readonly MemberProfilesTestDatabase _database = new();
    private readonly InMemoryMediaFileSystem _media = new();
    private readonly SettableTime _time = new(Now);

    public ProfileActivityLinkLookupTests()
    {
        using var context = _database.CreateDbContext();
        context.Database.Migrate();
    }

    public void Dispose() => _database.Dispose();

    /// <summary>A lookup service whose page reads go to <paramref name="respond"/> (every read fails when it is null).</summary>
    internal static ProfileActivityLinkLookupService Create(
        MemberProfilesTestDatabase database,
        InMemoryMediaFileSystem media,
        Func<HttpRequestMessage, HttpResponseMessage>? respond = null,
        TimeProvider? time = null,
        ProfileTimelineImageStoreTests.StubHandler? handler = null)
    {
        handler ??= new ProfileTimelineImageStoreTests.StubHandler(respond ?? (_ => new HttpResponseMessage(HttpStatusCode.NotFound)));
        var client = new LinkPreviewHttpClient(new HttpClient(handler));
        var caches = new AppCaches(new ObjectCacheAppCache(), NoAppCache.Instance, new IsolatedCaches(_ => NoAppCache.Instance));
        time ??= TimeProvider.System;
        return new ProfileActivityLinkLookupService(
            new LinkPreviewService(client, NullLogger<LinkPreviewService>.Instance),
            new ProfileTimelineImageStore(media.FileSystem, client, caches, NullLogger<ProfileTimelineImageStore>.Instance),
            new ProfileTimelinePreviewStore(database),
            new ProfileActivityLinkLookupThrottle(time),
            caches,
            time,
            NullLogger<ProfileActivityLinkLookupService>.Instance);
    }

    private (ProfileActivityLinkLookupService Service, ProfileTimelineImageStoreTests.StubHandler Handler) Create(
        Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var handler = new ProfileTimelineImageStoreTests.StubHandler(respond);
        return (Create(_database, _media, time: _time, handler: handler), handler);
    }

    private static HttpResponseMessage Html(string html) =>
        new(HttpStatusCode.OK) { Content = new StringContent(html, Encoding.UTF8, "text/html") };

    private const string FullPage = """
        <html><head>
          <title>Fallback title</title>
          <meta property="og:title" content="Building a package &amp; shipping it" />
          <meta property="og:type" content="article" />
          <meta property="og:description" content="How I built it." />
          <meta property="og:image" content="/cover.png" />
          <meta property="article:published_time" content="2026-10-03T21:30:00-05:00" />
        </head><body>Hi</body></html>
        """;

    // ---- reading a page ----

    [Fact]
    public void A_page_gives_its_title_description_picture_type_and_published_date()
    {
        var preview = LinkPreviewHtmlParser.Parse(FullPage, new Uri(Host + "/post"));

        preview.Title.Should().Be("Building a package & shipping it");
        preview.Description.Should().Be("How I built it.");
        preview.ImageUrl.Should().Be(Host + "/cover.png");
        preview.PageType.Should().Be("article");
        // The day as the page writes it, not moved to UTC.
        preview.PublishedDate.Should().Be(new DateOnly(2026, 10, 3));
    }

    [Fact]
    public void The_title_falls_back_to_twitter_then_the_title_tag()
    {
        LinkPreviewHtmlParser.Parse("""<head><meta name="twitter:title" content="Tweet title"><title>Tag</title></head>""", new Uri(Host))
            .Title.Should().Be("Tweet title");
        LinkPreviewHtmlParser.Parse("<head><title> Tag title </title></head>", new Uri(Host)).Title.Should().Be("Tag title");
    }

    [Theory]
    [InlineData("""<head><meta itemprop="datePublished" content="2026-09-01"></head>""")]
    [InlineData("""<head><script type="application/ld+json">{"@type":"BlogPosting","datePublished": "2026-09-01T08:00:00Z"}</script></head>""")]
    [InlineData("""<head></head><body><article><time class="x" datetime="2026-09-01">1 Sep</time></article></body>""")]
    [InlineData("""<head><meta property="og:published_time" content="2026-09-01T00:00:00Z"></head>""")]
    public void The_published_date_is_found_where_pages_put_it(string html)
    {
        LinkPreviewHtmlParser.Parse(html, new Uri(Host)).PublishedDate.Should().Be(new DateOnly(2026, 9, 1));
    }

    [Fact]
    public void A_date_that_is_not_a_date_is_ignored()
    {
        LinkPreviewHtmlParser.Parse("""<head><meta property="article:published_time" content="last tuesday"></head>""", new Uri(Host))
            .PublishedDate.Should().BeNull();
    }

    [Theory]
    [InlineData("https://www.youtube.com/watch?v=1", null, "videos")]
    [InlineData("https://youtu.be/1", null, "videos")]
    [InlineData("https://vimeo.com/1", "article", "videos")]
    [InlineData("https://www.nuget.org/packages/Our.Package", null, "packages")]
    [InlineData("https://marketplace.umbraco.com/package/our.package", null, "packages")]
    [InlineData("https://example.dev/post", "article", "blog")]
    [InlineData("https://example.dev/clip", "video.other", "videos")]
    [InlineData("https://example.dev/", "website", null)]
    [InlineData("https://notyoutube.com/", null, null)]
    public void The_kind_is_guessed_from_the_site_and_page_type(string url, string? pageType, string? kind)
    {
        ProfileActivityLinkLookupService.GuessKind(new Uri(url), pageType).Should().Be(kind);
    }

    // ---- looking a link up ----

    [Fact]
    public async Task A_page_fills_in_everything_with_its_picture_copied_to_this_site()
    {
        var (service, _) = Create(request => request.RequestUri!.AbsolutePath == "/cover.png"
            ? ProfileTimelineImageStoreTests.Image(ProfileTimelineImageStoreTests.Png)
            : Html(FullPage));

        var details = await service.LookUpAsync(Guid.NewGuid(), Host + "/post");

        details.Kind.Should().Be(ProfileActivityEntry.Blog);
        details.Title.Should().Be("Building a package & shipping it");
        details.Date.Should().Be(new DateOnly(2026, 10, 3));
        details.Description.Should().Be("How I built it.");
        var path = $"{ProfileTimelineImageStore.Folder}/{ProfileTimelineImageStore.FileName(Host + "/cover.png")}.png";
        details.ImageUrl.Should().Be("/media/" + path);
        _media.Files.Should().ContainKey(path);
        // Kept for the timeline card, so it needs no outside work later.
        (await new ProfileTimelinePreviewStore(_database).GetAsync([Host + "/post"])).Should().ContainKey(Host + "/post");
    }

    [Fact]
    public async Task Only_what_the_page_offers_is_returned_cut_to_the_entry_limits()
    {
        var longTitle = new string('t', ProfileActivityEntry.MaxTitleLength + 50);
        var (service, _) = Create(_ => Html($"<head><title>{longTitle}</title><meta property=\"article:published_time\" content=\"2027-01-01\"></head>"));

        var details = await service.LookUpAsync(Guid.NewGuid(), Host + "/partial");

        details.Title!.Length.Should().Be(ProfileActivityEntry.MaxTitleLength);
        details.Description.Should().BeNull();
        details.ImageUrl.Should().BeNull();
        details.Kind.Should().BeNull();
        // A date in the future isn't one the member could add.
        details.Date.Should().BeNull();
    }

    [Fact]
    public async Task A_page_that_cannot_be_read_gives_an_empty_answer()
    {
        var (service, _) = Create(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        (await service.LookUpAsync(Guid.NewGuid(), Host + "/down")).Should().Be(ActivityLinkDetails.Empty);
    }

    [Fact]
    public async Task A_picture_that_cannot_be_copied_is_left_out()
    {
        var (service, _) = Create(request => request.RequestUri!.AbsolutePath == "/cover.png"
            ? new HttpResponseMessage(HttpStatusCode.NotFound)
            : Html(FullPage));

        var details = await service.LookUpAsync(Guid.NewGuid(), Host + "/post");

        details.Title.Should().NotBeNull();
        details.ImageUrl.Should().BeNull();
    }

    [Theory]
    [InlineData("http://93.184.216.34/plain")]
    [InlineData("https://127.0.0.1/")]
    [InlineData("https://10.0.0.5/inside")]
    [InlineData("https://169.254.169.254/latest/meta-data")]
    [InlineData("https://user:pw@93.184.216.34/")]
    [InlineData("not a link")]
    [InlineData(null)]
    public async Task Addresses_that_are_not_https_or_not_public_are_never_read(string? url)
    {
        var (service, handler) = Create(_ => Html(FullPage));

        (await service.LookUpAsync(Guid.NewGuid(), url)).Should().Be(ActivityLinkDetails.Empty);

        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Each_member_may_have_only_a_few_pages_read_a_minute()
    {
        var (service, handler) = Create(_ => Html("<head><title>T</title></head>"));
        var member = Guid.NewGuid();
        for (var i = 0; i < ProfileActivityLinkLookupService.MaxLookupsPerWindow; i++)
        {
            (await service.LookUpAsync(member, $"{Host}/page-{i}")).Title.Should().Be("T");
        }

        (await service.LookUpAsync(member, Host + "/one-more")).Should().Be(ActivityLinkDetails.Empty);
        handler.Requests.Should().HaveCount(ProfileActivityLinkLookupService.MaxLookupsPerWindow);

        // Another member isn't held back, and the member can go on after a minute.
        (await service.LookUpAsync(Guid.NewGuid(), Host + "/someone-else")).Title.Should().Be("T");
        _time.Advance(ProfileActivityLinkLookupService.LookupWindow);
        (await service.LookUpAsync(member, Host + "/later")).Title.Should().Be("T");
    }

    [Fact]
    public async Task An_address_is_read_once_and_the_answer_reused_without_counting()
    {
        var (service, handler) = Create(_ => Html("<head><title>T</title></head>"));
        var member = Guid.NewGuid();

        for (var i = 0; i < ProfileActivityLinkLookupService.MaxLookupsPerWindow + 5; i++)
        {
            (await service.LookUpAsync(member, Host + "/same")).Title.Should().Be("T");
        }

        handler.Requests.Should().HaveCount(1);
    }

    // ---- the endpoint ----

    private async Task<ProfileActivityEntryApiController> ControllerAsync(bool inEarlyAccess, ProfileActivityLinkLookupService lookup)
    {
        var memberKey = Guid.NewGuid();
        var store = new MemberProfileStore(_database);
        await store.GetOrStartOnboardingAsync(memberKey, "octocat", "The Octocat");
        await store.CompleteOnboardingAsync(memberKey);

        var member = new MemberIdentityUser { Key = memberKey, UserName = "octocat" };
        var memberManager = new Mock<IMemberManager>();
        memberManager.Setup(m => m.IsLoggedIn()).Returns(true);
        memberManager.Setup(m => m.GetCurrentMemberAsync()).ReturnsAsync(member);
        memberManager.Setup(m => m.GetRolesAsync(member))
            .ReturnsAsync(new List<string> { inEarlyAccess ? ProfileAccessService.EarlyAccessGroupName : "Community Members" });
        var provider = new MemberProfileDataProvider(
            store,
            new ProfileAvatarUrlResolver(Mock.Of<IPublishedContentQuery>(), Mock.Of<IPublishedUrlProvider>()),
            TestImageProxy.Create());
        var images = new ProfileActivityImageStore(_media.FileSystem, NullLogger<ProfileActivityImageStore>.Instance);
        var entries = new ProfileActivityEntryService(
            new MemberActivityEntryStore(_database),
            Mock.Of<IProfileSyncClient>(),
            images,
            new AppCaches(new ObjectCacheAppCache(), NoAppCache.Instance, new IsolatedCaches(_ => NoAppCache.Instance)),
            NullLogger<ProfileActivityEntryService>.Instance);
        return new ProfileActivityEntryApiController(
            memberManager.Object,
            store,
            new ProfileAccessService(memberManager.Object, provider, NullLogger<ProfileAccessService>.Instance),
            entries,
            images,
            lookup);
    }

    [Fact]
    public async Task The_endpoint_answers_with_what_the_page_offers()
    {
        var (service, _) = Create(_ => Html("<head><title>From the page</title></head>"));
        var controller = await ControllerAsync(inEarlyAccess: true, service);

        var result = await controller.Preview(new LookUpActivityLinkRequest(Host + "/x"));

        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeOfType<ActivityLinkDetails>()
            .Which.Title.Should().Be("From the page");
    }

    [Fact]
    public async Task The_endpoint_gives_an_empty_answer_for_a_link_that_cannot_be_read()
    {
        var (service, _) = Create(_ => Html(FullPage));
        var controller = await ControllerAsync(inEarlyAccess: true, service);

        (await controller.Preview(new LookUpActivityLinkRequest("http://93.184.216.34/x"))).Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().Be(ActivityLinkDetails.Empty);
    }

    [Fact]
    public async Task Members_who_cannot_add_activity_cannot_look_links_up()
    {
        var (service, handler) = Create(_ => Html(FullPage));
        var controller = await ControllerAsync(inEarlyAccess: false, service);

        (await controller.Preview(new LookUpActivityLinkRequest(Host + "/x"))).Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        handler.Requests.Should().BeEmpty();
    }

    private sealed class SettableTime(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }
}

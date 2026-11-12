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

public class ProfileTimelinePreviewServiceTests : IDisposable
{
    // Address literals need no DNS lookup, so these tests never touch the network.
    private const string Host = "https://93.184.216.34";

    private static readonly DateTimeOffset Now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly ProfileDisplayChoices CardsOn = ProfileDisplayChoices.None;

    private readonly MemberProfilesTestDatabase _database = new();
    private readonly InMemoryMediaFileSystem _media = new();

    public ProfileTimelinePreviewServiceTests()
    {
        using var context = _database.CreateDbContext();
        context.Database.Migrate();
    }

    public void Dispose() => _database.Dispose();

    // Each call is a fresh server: its own memory, sharing the database and the media file system.
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

    // Pages offer a share picture at /share-{page}.png, with a description.
    private static HttpResponseMessage Site(HttpRequestMessage request)
    {
        var path = request.RequestUri!.AbsolutePath;
        return path.EndsWith(".png", StringComparison.Ordinal)
            ? ProfileTimelineImageStoreTests.Image(ProfileTimelineImageStoreTests.Png)
            : Html($"""<meta property="og:image" content="/share-{path.Trim('/')}.png"><meta property="og:description" content="What the page says it is">""");
    }

    private static int Pages(StubHandler handler) => handler.Requests.Count(r => !r.AbsolutePath.EndsWith(".png", StringComparison.Ordinal));

    private static ProfileTimelineEntry Entry(string id, string? url = null, string? imageUrl = null, string? description = null) =>
        new(PlatformActivityGroups.Blog, id, $"Title {id}", url ?? $"{Host}/{id}", description, Now, null, imageUrl);

    [Fact]
    public async Task An_entry_whose_page_offers_a_picture_gets_a_card_with_our_stored_copy_of_it()
    {
        var (service, _) = Create(Site);

        var result = await service.AddCardsAsync([Entry("b1")], CardsOn, CancellationToken.None);

        var card = result.Single().Card;
        card.Should().NotBeNull();
        card!.ImageUrl.Should().StartWith($"/media/{ProfileTimelineImageStore.Folder}/").And.EndWith(".png");
        card.Title.Should().Be("Title b1");
        card.Description.Should().Be("What the page says it is");
        _media.Files.Should().ContainSingle();
    }

    [Fact]
    public async Task The_entrys_own_description_is_preferred_over_the_pages()
    {
        var (service, _) = Create(Site);

        var result = await service.AddCardsAsync([Entry("b1", description: "In the member's words")], CardsOn, CancellationToken.None);

        result.Single().Card!.Description.Should().Be("In the member's words");
    }

    [Fact]
    public async Task An_entry_whose_page_has_no_picture_stays_a_plain_line()
    {
        var (service, _) = Create(_ => Html("<title>No picture here</title>"));

        var result = await service.AddCardsAsync([Entry("b1")], CardsOn, CancellationToken.None);

        result.Single().Card.Should().BeNull();
    }

    [Fact]
    public async Task A_video_thumbnail_already_known_is_stored_too_without_reading_the_page()
    {
        var (service, handler) = Create(Site);

        var result = await service.AddCardsAsync([Entry("v1", imageUrl: $"{Host}/thumb.png")], CardsOn, CancellationToken.None);

        result.Single().Card!.ImageUrl.Should().StartWith($"/media/{ProfileTimelineImageStore.Folder}/");
        handler.Requests.Should().ContainSingle().Which.AbsolutePath.Should().Be("/thumb.png");
    }

    [Fact]
    public async Task With_cards_switched_off_for_the_timeline_every_entry_is_a_plain_line_and_nothing_is_fetched()
    {
        var (service, handler) = Create(Site);
        var choices = ProfileDisplayChoices.None with { HideTimelineCards = true };

        var result = await service.AddCardsAsync([Entry("b1"), Entry("v1", imageUrl: $"{Host}/thumb.png")], choices, CancellationToken.None);

        result.Should().OnlyContain(e => e.Card == null);
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task An_entry_with_its_card_switched_off_is_a_plain_line_and_the_others_keep_theirs()
    {
        var (service, handler) = Create(Site);
        var choices = ProfileDisplayChoices.None with { NoCardActivityIds = ["b1", "v1"] };

        var result = await service.AddCardsAsync(
            [Entry("b1"), Entry("b2"), Entry("v1", imageUrl: $"{Host}/thumb.png")],
            choices,
            CancellationToken.None);

        result.Select(e => e.Card != null).Should().Equal(false, true, false);
        handler.Requests.Select(r => r.AbsolutePath).Should().BeEquivalentTo("/b2", "/share-b2.png");
    }

    [Fact]
    public async Task Pages_that_are_not_https_or_point_inside_are_never_fetched()
    {
        var (service, handler) = Create(Site);

        var result = await service.AddCardsAsync(
            [Entry("a", url: "http://93.184.216.34/a"), Entry("b", url: "https://10.0.0.5/b")],
            CardsOn,
            CancellationToken.None);

        result.Should().OnlyContain(e => e.Card == null);
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task A_render_does_outside_work_for_only_a_few_entries_and_the_rest_follow_on_a_later_render()
    {
        var (service, handler) = Create(Site);
        var entries = Enumerable.Range(1, ProfileTimelinePreviewService.MaxFetchesPerRender + 3).Select(i => Entry($"b{i}")).ToList();

        var first = await service.AddCardsAsync(entries, CardsOn, CancellationToken.None);

        Pages(handler).Should().Be(ProfileTimelinePreviewService.MaxFetchesPerRender);
        first.Take(ProfileTimelinePreviewService.MaxFetchesPerRender).Should().OnlyContain(e => e.Card != null);
        first.Skip(ProfileTimelinePreviewService.MaxFetchesPerRender).Should().OnlyContain(e => e.Card == null);

        var second = await service.AddCardsAsync(entries, CardsOn, CancellationToken.None);

        second.Should().OnlyContain(e => e.Card != null);
        Pages(handler).Should().Be(entries.Count);
    }

    [Fact]
    public async Task A_failing_page_is_remembered_and_not_fetched_again_straight_away()
    {
        var (service, handler) = Create(request => request.RequestUri!.AbsolutePath == "/broken"
            ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
            : Site(request));
        var entries = new[] { Entry("ok"), Entry("broken") };

        await service.AddCardsAsync(entries, CardsOn, CancellationToken.None);
        var again = await service.AddCardsAsync(entries, CardsOn, CancellationToken.None);

        handler.Requests.Should().HaveCount(3);
        again.Select(e => e.Card != null).Should().Equal(true, false);
    }

    [Fact]
    public async Task A_card_keeps_working_after_its_page_and_picture_disappear()
    {
        var (service, _) = Create(Site);
        var stored = (await service.AddCardsAsync([Entry("b1")], CardsOn, CancellationToken.None)).Single().Card;

        // Later, on a fresh server, the whole site is gone.
        var (later, handler) = Create(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var result = await later.AddCardsAsync([Entry("b1")], CardsOn, CancellationToken.None);

        result.Single().Card.Should().Be(stored);
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task A_page_that_throws_falls_back_to_a_plain_line()
    {
        var (service, _) = Create(_ => throw new HttpRequestException("boom"));

        var result = await service.AddCardsAsync([Entry("b1")], CardsOn, CancellationToken.None);

        result.Single().Card.Should().BeNull();
    }

    [Fact]
    public async Task Entries_keep_their_order()
    {
        var (service, _) = Create(Site);
        var entries = new[] { Entry("b1"), Entry("b2", url: "http://93.184.216.34/b2"), Entry("b3") };

        var result = await service.AddCardsAsync(entries, CardsOn, CancellationToken.None);

        result.Select(e => e.ActivityId).Should().Equal("b1", "b2", "b3");
        result.Select(e => e.Card != null).Should().Equal(true, false, true);
    }

    [Fact]
    public void A_card_needs_a_stored_picture()
    {
        var entry = Entry("b1", description: "Mine");

        ProfileTimelinePreviewService.Card(entry, null, "Page").Should().BeNull();
        ProfileTimelinePreviewService.Card(entry, "/media/x.png", "Page").Should().Be(new ProfileTimelineCard("/media/x.png", "Title b1", "Mine"));
    }

    [Fact]
    public void An_entry_without_a_page_never_wants_a_card()
    {
        var meetup = new ProfileTimelineEntry(PlatformActivityGroups.Meetups, "m1", "A meet-up", null, null, Now, null, null);

        ProfileTimelinePreviewService.WantsCard(CardsOn, meetup).Should().BeFalse();
        ProfileTimelinePreviewService.WantsCard(CardsOn, Entry("b1")).Should().BeTrue();
    }
}

using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using UmbracoCommunity.Web.Features.Profiles.Data.Entities;
using UmbracoCommunity.Web.Features.Profiles.ForumBadges;
using UmbracoCommunity.Web.Tests.Features.Feeds.Calendar;
using Xunit;

namespace UmbracoCommunity.Web.Tests.Features.Profiles;

public class ForumBadgeClientTests
{
    /// <summary>
    /// A made-up answer in the shape of the forum's <c>user-badges/{username}.json</c> (Discourse): the badge
    /// definitions, the badge types and the user's grants. Badge 5 was granted twice; badge 9 is switched off; the grant of
    /// badge 404 has no definition.
    /// </summary>
    internal const string UserBadgesJson = """
        {
          "badges": [
            { "id": 2, "name": "Member", "description": "<a href=\"https://example.com\">Granted</a> invitations &amp; more", "grant_count": 103, "icon": "user", "image_url": null, "enabled": true, "badge_type_id": 3 },
            { "id": 107, "name": "Umbraco MVP", "description": "An Umbraco MVP", "grant_count": 113, "icon": "", "image_url": "/uploads/default/original/1X/mvp.png", "enabled": true, "badge_type_id": 1, "slug": "umbraco-mvp" },
            { "id": 5, "name": "Nice Reply", "description": null, "grant_count": 40, "icon": "heart", "image_url": "https://cdn.example.com/nice.png", "enabled": true, "badge_type_id": 3, "slug": "../../evil?x=1" },
            { "id": 8, "name": "Great Topic", "description": "", "grant_count": 3, "icon": "star", "image_url": "javascript:alert(1)", "enabled": true, "badge_type_id": 2 },
            { "id": 9, "name": "Retired", "description": "Gone", "grant_count": 1, "icon": "x", "image_url": null, "enabled": false, "badge_type_id": 1 }
          ],
          "badge_types": [
            { "id": 3, "name": "Bronze", "sort_order": 7 },
            { "id": 1, "name": "Gold", "sort_order": 9 },
            { "id": 2, "name": "Silver", "sort_order": 8 }
          ],
          "granted_bies": [],
          "user_badges": [
            { "id": 1, "granted_at": "2025-02-06T06:47:25.253Z", "badge_id": 2, "granted_by_id": -1 },
            { "id": 2, "granted_at": "2025-06-01T00:00:00Z", "badge_id": 107, "granted_by_id": -1 },
            { "id": 3, "granted_at": "2025-08-01T00:00:00Z", "badge_id": 5, "granted_by_id": -1 },
            { "id": 4, "granted_at": "2025-03-01T00:00:00Z", "badge_id": 5, "granted_by_id": -1 },
            { "id": 5, "granted_at": "2025-04-01T00:00:00Z", "badge_id": 8, "granted_by_id": -1 },
            { "id": 6, "granted_at": "2025-04-01T00:00:00Z", "badge_id": 9, "granted_by_id": -1 },
            { "id": 7, "granted_at": "2025-04-01T00:00:00Z", "badge_id": 404, "granted_by_id": -1 }
          ]
        }
        """;

    internal static ForumBadgeClient CreateClient(HttpMessageHandler handler, IMemoryCache? cache = null) =>
        new(
            new HttpClient(handler) { BaseAddress = new Uri("https://forum.umbraco.com/") },
            cache ?? new MemoryCache(new MemoryCacheOptions()),
            NullLogger<ForumBadgeClient>.Instance);

    [Fact]
    public async Task Reads_the_users_badges_from_the_public_endpoint()
    {
        Uri? requested = null;
        var handler = new StubHandler(request =>
        {
            requested = request.RequestUri;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(UserBadgesJson) };
        });

        var result = await CreateClient(handler).GetBadgesAsync("octocat");

        requested.Should().Be(new Uri("https://forum.umbraco.com/user-badges/octocat.json"));
        result.Status.Should().Be(ForumBadgesStatus.Ok);
        result.Badges.Select(b => b.Name).Should().Equal("Umbraco MVP", "Great Topic", "Member", "Nice Reply");
    }

    [Fact]
    public void Parses_each_badge_once_with_plain_text_and_safe_picture_addresses()
    {
        using var document = JsonDocument.Parse(UserBadgesJson);

        var badges = ForumBadgeClient.Parse(document.RootElement).ToDictionary(b => b.Id);

        badges.Keys.Should().BeEquivalentTo([107, 8, 2, 5], "the switched-off badge and the grant without a definition are skipped");

        badges[107].ImageUrl.Should().Be("https://forum.umbraco.com/uploads/default/original/1X/mvp.png");
        badges[107].BadgeType.Should().Be("Gold");
        badges[107].GrantCount.Should().Be(113);

        badges[2].Description.Should().Be("Granted invitations & more");
        badges[2].ImageUrl.Should().BeNull();

        badges[5].ImageUrl.Should().Be("https://cdn.example.com/nice.png");
        badges[5].GrantedAt.Should().Be(new DateTimeOffset(2025, 3, 1, 0, 0, 0, TimeSpan.Zero), "its first grant counts");
        badges[5].Description.Should().BeNull();

        badges[8].ImageUrl.Should().BeNull("only https pictures are used");

        // Links to the badges' pages are built from id and slug on the forum's own host.
        badges[107].Url.Should().Be("https://forum.umbraco.com/badges/107/umbraco-mvp");
        badges[5].Url.Should().Be("https://forum.umbraco.com/badges/5/nice-reply", "a slug that isn't plain is made from the name");
        badges[8].Url.Should().Be("https://forum.umbraco.com/badges/8/great-topic", "a missing slug is made from the name");
        badges[8].Description.Should().BeNull();
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{ "badges": "nope", "user_badges": 3 }""")]
    [InlineData("""{ "badges": [ { "id": "x" } ], "user_badges": [ { "badge_id": null }, 7, "a" ] }""")]
    [InlineData("""{ "badges": [ { "id": 1 } ], "user_badges": [ { "badge_id": 1 } ] }""")]
    public async Task An_answer_with_nothing_usable_is_an_empty_list(string body)
    {
        var handler = StubHandler.Json(body);

        var result = await CreateClient(handler).GetBadgesAsync("octocat");

        result.Status.Should().Be(ForumBadgesStatus.Ok);
        result.Badges.Should().BeEmpty();
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("")]
    public async Task An_answer_that_is_not_a_json_object_is_unavailable(string body)
    {
        var result = await CreateClient(StubHandler.Json(body)).GetBadgesAsync("octocat");

        result.Status.Should().Be(ForumBadgesStatus.Unavailable);
        result.Badges.Should().BeEmpty();
    }

    [Fact]
    public async Task An_unknown_user_is_not_found()
    {
        var result = await CreateClient(StubHandler.Status(HttpStatusCode.NotFound)).GetBadgesAsync("nobody");

        result.Status.Should().Be(ForumBadgesStatus.NotFound);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task An_error_answer_is_unavailable(HttpStatusCode status)
    {
        var result = await CreateClient(StubHandler.Status(status)).GetBadgesAsync("octocat");

        result.Status.Should().Be(ForumBadgesStatus.Unavailable);
    }

    [Fact]
    public async Task A_network_failure_is_unavailable_not_thrown()
    {
        var result = await CreateClient(StubHandler.Throws()).GetBadgesAsync("octocat");

        result.Status.Should().Be(ForumBadgesStatus.Unavailable);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("..")]
    [InlineData("a/b")]
    [InlineData("a?b")]
    [InlineData("a%2fb")]
    [InlineData("名前")]
    [InlineData("with space")]
    public async Task A_username_that_is_not_a_plain_forum_username_is_never_asked_for(string? username)
    {
        var handler = StubHandler.Json(UserBadgesJson);

        var result = await CreateClient(handler).GetBadgesAsync(username);

        result.Status.Should().Be(ForumBadgesStatus.NotFound);
        handler.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Answers_are_kept_per_username_ignoring_case()
    {
        var handler = StubHandler.Json(UserBadgesJson);
        var client = CreateClient(handler);

        await client.GetBadgesAsync("Octocat");
        var again = await client.GetBadgesAsync("octocat");

        handler.CallCount.Should().Be(1);
        again.Badges.Should().HaveCount(4);
    }

    [Fact]
    public async Task A_failure_is_kept_too_so_a_broken_forum_is_not_asked_on_every_page_view()
    {
        var handler = StubHandler.Status(HttpStatusCode.BadGateway);
        var client = CreateClient(handler);

        await client.GetBadgesAsync("octocat");
        var again = await client.GetBadgesAsync("octocat");

        handler.CallCount.Should().Be(1);
        again.Status.Should().Be(ForumBadgesStatus.Unavailable);
        ForumBadgeClient.FailureCacheDuration.Should().BeLessThan(ForumBadgeClient.CacheDuration);
    }

    [Theory]
    [InlineData("octocat", true)]
    [InlineData("Jane.Doe-1_x", true)]
    [InlineData(".", false)]
    [InlineData("...", false)]
    [InlineData("a b", false)]
    [InlineData("a/../b", false)]
    [InlineData("a\\b", false)]
    public void Only_plain_usernames_are_valid(string username, bool valid) =>
        ForumProfileUrl.IsValidUsername(username).Should().Be(valid);

    [Fact]
    public void A_username_longer_than_the_forum_allows_is_not_valid()
    {
        ForumProfileUrl.IsValidUsername(new string('a', ForumProfileUrl.MaxUsernameLength)).Should().BeTrue();
        ForumProfileUrl.IsValidUsername(new string('a', ForumProfileUrl.MaxUsernameLength + 1)).Should().BeFalse();
    }

    [Theory]
    [InlineData("https://forum.umbraco.com/u/sebastiaan", "sebastiaan")]
    [InlineData("https://FORUM.umbraco.com/u/Jane.Doe-1/summary", "Jane.Doe-1")]
    [InlineData("https://forum.umbraco.com/U/jane/activity?x=1#y", "jane")]
    [InlineData("https://forum.umbraco.com/u/jane/", "jane")]
    [InlineData("  https://forum.umbraco.com/u/jane?tab=badges  ", "jane")]
    public void Reads_the_username_from_a_forum_profile_link(string url, string username)
    {
        ForumProfileUrl.TryGetUsername(url, out var found).Should().BeTrue();
        found.Should().Be(username);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a url")]
    [InlineData("http://forum.umbraco.com/u/jane")]
    [InlineData("https://forum.umbraco.com:8443/u/jane")]
    [InlineData("https://forum.umbraco.com.evil.example/u/jane")]
    [InlineData("https://evil.example/u/jane")]
    [InlineData("https://forum.umbraco.com/t/some-topic/123")]
    [InlineData("https://forum.umbraco.com/u/")]
    [InlineData("https://forum.umbraco.com/u/..")]
    [InlineData("https://forum.umbraco.com/u/a%2Fb")]
    [InlineData("https://forum.umbraco.com/u/a%20b")]
    public void Ignores_any_other_link(string? url)
    {
        ForumProfileUrl.TryGetUsername(url, out var found).Should().BeFalse();
        found.Should().BeEmpty();
    }

    [Fact]
    public void Finds_the_first_shown_forum_link_then_a_hidden_one_and_never_a_removed_one()
    {
        static MemberFeedEntity Feed(int id, string url, int order, bool hidden = false, bool removed = false) =>
            new() { Id = id, Platform = "Forum", Url = url, SortOrder = order, IsHidden = hidden, IsRemoved = removed };

        ForumProfileUrl.FindUsername([
            Feed(1, "https://forum.umbraco.com/u/removed", 0, removed: true),
            Feed(2, "https://forum.umbraco.com/u/hidden", 1, hidden: true),
            Feed(3, "https://octo.example", 2),
            Feed(4, "https://forum.umbraco.com/u/shown", 3),
            Feed(5, "https://forum.umbraco.com/u/later", 4),
        ]).Should().Be("shown");

        ForumProfileUrl.FindUsername([
            Feed(1, "https://forum.umbraco.com/u/removed", 0, removed: true),
            Feed(2, "https://forum.umbraco.com/u/hidden", 1, hidden: true),
        ]).Should().Be("hidden");

        ForumProfileUrl.FindUsername([Feed(1, "https://octo.example", 0)]).Should().BeNull();
        ForumProfileUrl.FindUsername([]).Should().BeNull();
    }

    [Theory]
    [InlineData("umbraco-mvp", "Ignored", "umbraco-mvp")]
    [InlineData("v2-launch-2026", "Ignored", "v2-launch-2026")]
    [InlineData(null, "Umbraco MVP", "umbraco-mvp")]
    [InlineData("", "  Café & Co.!  ", "caf-co")]
    [InlineData("UPPER", "Upper Case", "upper-case")]
    [InlineData("a--b", "A B", "a-b")]
    [InlineData("-lead", "Lead", "lead")]
    [InlineData("../../evil", "Evil", "evil")]
    [InlineData("x/y", "x/y", "x-y")]
    [InlineData("javascript:alert(1)", "Alert", "alert")]
    [InlineData(null, "名前", null)]
    [InlineData(null, "!!!", null)]
    [InlineData(null, null, null)]
    public void Badge_slugs_are_plain_or_made_from_the_name(string? forumSlug, string? name, string? expected) =>
        ForumProfileUrl.BadgeSlug(forumSlug, name).Should().Be(expected);

    [Fact]
    public void A_long_badge_slug_is_cut_to_the_limit()
    {
        var slug = ForumProfileUrl.BadgeSlug(null, string.Join(" ", Enumerable.Repeat("word", 50)))!;

        slug.Length.Should().BeLessThanOrEqualTo(ForumProfileUrl.MaxSlugLength);
        slug.Should().NotEndWith("-");
    }

    [Theory]
    [InlineData(107, "umbraco-mvp", "https://forum.umbraco.com/badges/107/umbraco-mvp")]
    [InlineData(107, null, "https://forum.umbraco.com/badges/107")]
    [InlineData(107, "", "https://forum.umbraco.com/badges/107")]
    [InlineData(107, "Not Plain", "https://forum.umbraco.com/badges/107")]
    [InlineData(107, "../admin", "https://forum.umbraco.com/badges/107")]
    [InlineData(107, "a?b=c", "https://forum.umbraco.com/badges/107")]
    [InlineData(0, "zero", null)]
    [InlineData(-3, "negative", null)]
    public void Badge_links_point_at_the_forums_badge_page(int id, string? slug, string? expected) =>
        ForumProfileUrl.BadgeUrl(id, slug).Should().Be(expected);

    [Fact]
    public void A_long_description_is_cut()
    {
        var text = ForumBadgeClient.PlainText(new string('x', 1000))!;

        text.Length.Should().Be(ForumBadgeClient.MaxDescriptionLength);
        text.Should().EndWith("…");
    }
}

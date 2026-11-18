using FluentAssertions;
using UmbracoCommunity.Web.Features.Profiles;
using UmbracoCommunity.Web.Features.Profiles.Models;
using Xunit;

namespace UmbracoCommunity.Web.Tests.Features.Profiles;

/// <summary>Which social posts could be the same post (time and platform only) and how confirmed pairs merge.</summary>
public class ProfileTimelineCrossPostTests
{
    private static ProfileTimelineEntry Post(string platform, string text, int day, string id) =>
        new(PlatformActivityGroups.Social, id, text, "https://" + platform + ".example/" + id, null, new DateTimeOffset(2026, 3, day, 9, 0, 0, TimeSpan.Zero), platform, null);

    private static HashSet<string> Confirmed(params (string A, string B)[] pairs) =>
        pairs.Select(p => ProfileTimeline.PairKey(p.A, p.B)).ToHashSet();

    [Fact]
    public void Without_a_model_answer_nothing_is_merged()
    {
        ProfileTimeline.MergeCrossPosts([Post("mastodon", "Same text", 20, "m"), Post("bluesky", "Same text", 20, "b")]).Should().HaveCount(2);
    }

    [Fact]
    public void A_confirmed_pair_becomes_one_entry_listing_both_platforms()
    {
        var merged = ProfileTimeline.MergeCrossPosts(
            [Post("mastodon", "one", 20, "m"), Post("bluesky", "two", 20, "b")], Confirmed(("b", "m")));

        merged.Should().ContainSingle().Which.Meta.Should().Be("bluesky, mastodon");
    }

    [Fact]
    public void The_newest_post_leads_the_merged_entry()
    {
        var merged = ProfileTimeline.MergeCrossPosts(
            [Post("mastodon", "one", 20, "m"), Post("bluesky", "two", 21, "b")], Confirmed(("b", "m")));

        merged.Should().ContainSingle().Which.ActivityId.Should().Be("b");
    }

    [Fact]
    public void A_copy_on_the_same_platform_is_merged_and_the_platform_listed_once()
    {
        var merged = ProfileTimeline.MergeCrossPosts(
            [Post("mastodon", "one", 20, "m1"), Post("bluesky", "two", 20, "b"), Post("mastodon", "three", 20, "m2")],
            Confirmed(("m1", "b"), ("m2", "m1")));

        merged.Should().ContainSingle().Which.Meta.Should().Be("bluesky, mastodon");
    }

    [Fact]
    public void Four_copies_join_one_entry_even_when_one_is_only_confirmed_against_a_middle_copy()
    {
        var posts = new[]
        {
            Post("bluesky", "a", 6, "b"),
            Post("mastodon", "b", 5, "m"),
            Post("linkedin", "c", 5, "l"),
            Post("twitter", "d", 4, "t"),
        };

        ProfileTimeline.MergeCrossPosts(posts, Confirmed(("b", "m"), ("m", "l"), ("l", "t")))
            .Should().ContainSingle().Which.Meta.Should().Be("bluesky, linkedin, mastodon, twitter");
    }

    [Fact]
    public void Only_posts_within_six_hours_of_each_other_are_candidates()
    {
        var pairs = ProfileTimeline.CandidatePairs([
            Post("mastodon", "one", 20, "m"),
            Post("bluesky", "two", 20, "b"),
            Post("bluesky", "three", 20, "b2"),
            Post("linkedin", "next day", 21, "l"),
        ]);

        pairs.Select(p => ProfileTimeline.PairKey(p.A.ActivityId, p.B.ActivityId))
            .Should().BeEquivalentTo(Confirmed(("b", "m"), ("b2", "m"), ("b", "b2")));
    }

    [Fact]
    public void Posts_without_an_id_or_text_are_not_candidates()
    {
        ProfileTimeline.CandidatePairs([Post("mastodon", "one", 20, ""), Post("bluesky", "  ", 20, "b")]).Should().BeEmpty();
    }
}

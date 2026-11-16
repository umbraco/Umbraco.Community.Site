using FluentAssertions;
using UmbracoCommunity.Web.Features.Profiles;
using UmbracoCommunity.Web.Features.Profiles.Models;
using Xunit;

namespace UmbracoCommunity.Web.Tests.Features.Profiles;

public class ProfileTimelineCrossPostTests
{
    private static ProfileTimelineEntry Post(string platform, string text, int day, string id) =>
        new(PlatformActivityGroups.Social, id, text, "https://" + platform + ".example/" + id, null, new DateTimeOffset(2026, 3, day, 9, 0, 0, TimeSpan.Zero), platform, null);

    private const string Bluesky = "The Umbraco Spark conference is drawing to a close and it's been a brilliant edition again this year. Great talks, record crowd, inspirational hackathon before, impeccable organisation.. am I using too many superlatives? #Umbraco";
    private const string Mastodon = "Attached: 4 images The Umbraco Spark conference is drawing to a close and it's been a brilliant edition again this year. Great talks, record attendance, impeccable organisation, inspirational hackathon before.. am I using too many superlatives? #Umbraco";

    [Fact]
    public void The_same_post_on_two_platforms_becomes_one_entry_listing_both()
    {
        var merged = ProfileTimeline.MergeCrossPosts([Post("mastodon", Mastodon, 20, "m"), Post("bluesky", Bluesky, 20, "b")]);

        merged.Should().ContainSingle().Which.Meta.Should().Be("bluesky, mastodon");
    }

    [Fact]
    public void Different_posts_stay_separate()
    {
        ProfileTimeline.MergeCrossPosts([Post("mastodon", "Off to the Dutch Umbraco festival, see you there!", 20, "m"), Post("bluesky", Bluesky, 20, "b")])
            .Should().HaveCount(2);
    }

    [Fact]
    public void Similar_posts_far_apart_or_on_the_same_platform_stay_separate()
    {
        ProfileTimeline.MergeCrossPosts([Post("mastodon", Bluesky, 1, "m"), Post("bluesky", Bluesky, 20, "b")]).Should().HaveCount(2);
        ProfileTimeline.MergeCrossPosts([Post("bluesky", Bluesky, 20, "a"), Post("bluesky", Bluesky, 20, "b")]).Should().HaveCount(2);
    }
}

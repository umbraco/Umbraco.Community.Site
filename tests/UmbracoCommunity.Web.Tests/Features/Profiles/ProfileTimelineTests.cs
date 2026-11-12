using FluentAssertions;
using UmbracoCommunity.Web.Features.Profiles;
using UmbracoCommunity.Web.Features.Profiles.Models;
using Xunit;

namespace UmbracoCommunity.Web.Tests.Features.Profiles;

public class ProfileTimelineTests
{
    private static DateTimeOffset On(int year, int month, int day) => new(year, month, day, 12, 0, 0, TimeSpan.Zero);

    private static CommunityProfile Profile(
        IReadOnlyList<ProfileBlogPost>? blogPosts = null,
        IReadOnlyList<ProfileVideo>? videos = null,
        IReadOnlyList<ProfilePackage>? packages = null,
        IReadOnlyList<ProfileMeetup>? meetups = null,
        IReadOnlyList<ProfileForumPost>? forumPosts = null,
        IReadOnlyList<ProfileSocialPost>? socialPosts = null,
        IReadOnlyList<ProfileOtherActivity>? other = null) =>
        new(
            Slug: "octocat",
            PlatformId: null,
            Identity: new ProfileIdentity("The Octocat", null, null, null, null, null, false, [], "octocat", null, [], false),
            BlogPosts: blogPosts ?? [],
            Videos: videos ?? [],
            ForumPosts: forumPosts ?? [],
            Packages: packages ?? [],
            Talks: [new ProfileTalk("A talk", "Codegarden", On(2026, 6, 10), null)],
            Badges: [],
            SocialPosts: socialPosts ?? [],
            Meetups: meetups ?? [])
        {
            OtherActivities = other ?? [],
        };

    private static CommunityProfile Everything() => Profile(
        blogPosts: [new ProfileBlogPost("A post", "https://blog.example/1", On(2026, 3, 2), "blog.example", "About things", "b1")],
        videos: [new ProfileVideo("A video", "https://video.example/1", "https://img.example/thumb.jpg", On(2026, 3, 20), ActivityId: "v1")],
        packages: [new ProfilePackage("A package", "https://packages.example/1", ["2.0.0", "1.0.0"], "Does things", "p1", On(2026, 1, 15))],
        meetups: [new ProfileMeetup("Attended", "UmbLeeds", "Umbraco Leeds", On(2025, 12, 5), "https://meetup.example/1", "m1")],
        forumPosts: [new ProfileForumPost("A thread", "https://forum.example/t/1", On(2026, 2, 1), 4, "f1")],
        socialPosts: [new ProfileSocialPost("Mastodon", "https://social.example/1", "Hello", On(2026, 2, 1), "s1")],
        other: [new ProfileOtherActivity("Podcast or interview", "A podcast", "https://podcast.example/1", On(2025, 6, 1), null, "added:1")]);

    [Fact]
    public void Every_dated_activity_is_on_the_timeline_newest_first_and_talks_are_not()
    {
        var entries = ProfileTimeline.Entries(Everything());

        entries.Select(e => e.ActivityId).Should().Equal("v1", "b1", "f1", "s1", "p1", "m1", "added:1");
        entries.Should().NotContain(e => e.Title == "A talk");
    }

    [Fact]
    public void Entries_are_grouped_by_month_with_three_letter_month_names_newest_month_first()
    {
        var months = ProfileTimeline.ByMonth(ProfileTimeline.Entries(Everything()));

        months.Select(m => m.Label).Should().Equal("Mar 2026", "Feb 2026", "Jan 2026", "Dec 2025", "Jun 2025");
        months.Select(m => m.Key).Should().Equal("2026-03", "2026-02", "2026-01", "2025-12", "2025-06");
        months[0].Entries.Select(e => e.ActivityId).Should().Equal("v1", "b1");
        months[1].Entries.Select(e => e.ActivityId).Should().Equal("f1", "s1");
    }

    [Fact]
    public void Each_entry_has_its_sections_icon_and_a_line_of_context()
    {
        var entries = ProfileTimeline.Entries(Everything()).ToDictionary(e => e.ActivityId!);

        entries["b1"].IconKey.Should().Be("blog");
        entries["b1"].Meta.Should().Be("blog.example");
        entries["v1"].IconKey.Should().Be("video");
        entries["v1"].ImageUrl.Should().Be("https://img.example/thumb.jpg");
        entries["p1"].IconKey.Should().Be("package");
        entries["p1"].Meta.Should().Be("v2.0.0");
        entries["m1"].IconKey.Should().Be("meetup");
        entries["m1"].Meta.Should().Be("Attended · Umbraco Leeds");
        entries["f1"].IconKey.Should().Be("forum");
        entries["f1"].Meta.Should().Be("4 replies");
        entries["s1"].IconKey.Should().Be("social");
        entries["added:1"].IconKey.Should().Be("other");
        entries["added:1"].Meta.Should().Be("Podcast or interview");
    }

    [Fact]
    public void A_package_without_a_date_is_left_out()
    {
        var entries = ProfileTimeline.Entries(Profile(packages: [new ProfilePackage("Undated", "https://packages.example/2", [], ActivityId: "p2")]));

        entries.Should().BeEmpty();
    }

    [Fact]
    public void Entries_carry_their_type_for_the_filter()
    {
        var entries = ProfileTimeline.Entries(Everything()).ToDictionary(e => e.ActivityId!);

        entries["b1"].Group.Should().Be(PlatformActivityGroups.Blog);
        entries["p1"].Group.Should().Be(PlatformActivityGroups.Packages);
        entries["added:1"].Group.Should().Be(PlatformActivityGroups.Other);
    }

    [Theory]
    [InlineData("blog", "blog")]
    [InlineData("BLOG", "blog")]
    [InlineData(" videos ", "videos")]
    [InlineData("talks", null)]
    [InlineData("forum", null)]
    [InlineData("<script>", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void The_type_in_the_address_is_used_only_when_it_is_one_of_the_filters_options(string? requested, string? selected)
    {
        var types = new[]
        {
            new ProfileTimelineType(PlatformActivityGroups.Blog, "Blog posts"),
            new ProfileTimelineType(PlatformActivityGroups.Videos, "Videos"),
        };

        ProfileTimeline.SelectedType(requested, types).Should().Be(selected);
    }

    [Fact]
    public void Filtering_by_type_shows_only_that_types_entries_and_no_type_shows_everything()
    {
        var entries = ProfileTimeline.Entries(Everything());

        entries.Where(e => ProfileTimeline.IsShown(e, PlatformActivityGroups.Blog)).Select(e => e.ActivityId).Should().Equal("b1");
        entries.Where(e => ProfileTimeline.IsShown(e, null)).Should().HaveCount(entries.Count);
    }

    [Fact]
    public void The_type_filter_offers_only_the_sections_that_have_entries_in_section_order()
    {
        var entries = ProfileTimeline.Entries(Profile(
            videos: [new ProfileVideo("A video", "https://video.example/1", null, On(2026, 3, 20), ActivityId: "v1")],
            blogPosts: [new ProfileBlogPost("A post", "https://blog.example/1", On(2026, 3, 2), null, ActivityId: "b1")]));

        ProfileTimeline.Types(entries).Should().Equal(
            new ProfileTimelineType(PlatformActivityGroups.Blog, "Blog posts"),
            new ProfileTimelineType(PlatformActivityGroups.Videos, "Videos"));
    }

    [Fact]
    public void Hidden_sections_and_items_stay_off_the_timeline()
    {
        var profile = Everything() with
        {
            DisplayChoices = new ProfileDisplayChoices([PlatformActivityGroups.Forum], ["b1"], []),
        };

        var entries = ProfileTimeline.Entries(ProfileDisplay.Apply(profile));

        entries.Select(e => e.ActivityId).Should().NotContain(["b1", "f1"]);
        entries.Should().HaveCount(5);
    }

    [Fact]
    public void A_devrel_view_that_shows_everything_keeps_hidden_items_on_the_timeline()
    {
        var profile = Everything() with
        {
            DisplayChoices = new ProfileDisplayChoices([PlatformActivityGroups.Forum], ["b1"], []),
        };

        var entries = ProfileTimeline.Entries(ProfileDisplay.Apply(profile, showHidden: true));

        entries.Select(e => e.ActivityId).Should().Contain(["b1", "f1"]);
    }

    [Fact]
    public void An_empty_profile_has_an_empty_timeline()
    {
        var entries = ProfileTimeline.Entries(Profile());

        entries.Should().BeEmpty();
        ProfileTimeline.ByMonth(entries).Should().BeEmpty();
        ProfileTimeline.Types(entries).Should().BeEmpty();
    }
}

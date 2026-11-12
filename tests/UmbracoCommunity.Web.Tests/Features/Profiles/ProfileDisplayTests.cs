using FluentAssertions;
using UmbracoCommunity.Web.Features.Profiles;
using UmbracoCommunity.Web.Features.Profiles.Data.Entities;
using UmbracoCommunity.Web.Features.Profiles.Models;
using Xunit;

namespace UmbracoCommunity.Web.Tests.Features.Profiles;

public class ProfileDisplayTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly CommunityProfile Profile = new(
        Slug: "octocat",
        PlatformId: null,
        Identity: new ProfileIdentity("The Octocat", null, null, null, null, null, false, [], "octocat", null, [], false),
        BlogPosts:
        [
            new ProfileBlogPost("First post", "https://example.dev/1", Now, "example.dev", "About things", ActivityId: "b1"),
            new ProfileBlogPost("Second post", "https://example.dev/2", Now.AddDays(-1), null, ActivityId: "b2"),
        ],
        Videos: [new ProfileVideo("A video", "https://video.example/1", null, Now, ActivityId: "v1")],
        ForumPosts: [new ProfileForumPost("A thread", "https://forum.example/t/1", Now, null, ActivityId: "f1")],
        Packages: [new ProfilePackage("A package", "https://packages.example/1", [], ActivityId: "p1")],
        Talks: [new ProfileTalk("A talk", "Codegarden", Now, null)],
        Badges: [],
        SocialPosts: [new ProfileSocialPost("Mastodon", "https://social.example/1", "Hello", Now, ActivityId: "s1")],
        Meetups: [new ProfileMeetup("Attended", "UmbLeeds", null, Now, "https://meetup.example/1", ActivityId: "m1")]);

    private static CommunityProfile With(ProfileDisplayChoices choices) => Profile with { DisplayChoices = choices };

    private static UpdateProfileDisplayRequest Request(
        IReadOnlyList<string>? hiddenSections = null,
        IReadOnlyList<string>? hiddenItemIds = null,
        params UpdateProfilePinRequest[] pins) =>
        new(hiddenSections, hiddenItemIds, pins);

    [Fact]
    public void Without_choices_the_profile_is_returned_as_it_is()
    {
        ProfileDisplay.Apply(Profile).Should().BeSameAs(Profile);
    }

    [Fact]
    public void A_switched_off_section_is_emptied_and_the_others_are_kept()
    {
        var result = ProfileDisplay.Apply(With(new ProfileDisplayChoices([PlatformActivityGroups.Blog], [], [])));

        result.BlogPosts.Should().BeEmpty();
        result.Videos.Should().HaveCount(1);
        result.ForumPosts.Should().HaveCount(1);
        result.Packages.Should().HaveCount(1);
        result.SocialPosts.Should().HaveCount(1);
        result.Meetups.Should().HaveCount(1);
        result.Talks.Should().HaveCount(1);
    }

    [Fact]
    public void A_hidden_item_is_removed_from_its_section()
    {
        var result = ProfileDisplay.Apply(With(new ProfileDisplayChoices([], ["b1", "m1"], [])));

        result.BlogPosts.Select(p => p.Title).Should().Equal("Second post");
        result.Meetups.Should().BeEmpty();
    }

    [Fact]
    public void Items_without_an_activity_id_are_never_hidden()
    {
        var profile = With(new ProfileDisplayChoices([], ["b1"], [])) with
        {
            BlogPosts = [new ProfileBlogPost("Curated", "https://example.dev/c", Now, null)],
        };

        ProfileDisplay.Apply(profile).BlogPosts.Should().ContainSingle();
    }

    [Fact]
    public void Pinned_items_are_featured_in_the_members_order_with_their_reason()
    {
        var result = ProfileDisplay.Apply(With(new ProfileDisplayChoices([], [],
        [
            new ProfilePin("v1", "My first video"),
            new ProfilePin("b2", null),
            new ProfilePin("p1", "Downloaded a lot"),
        ])));

        result.FeaturedItems.Select(i => i.ActivityId).Should().Equal("v1", "b2", "p1");
        result.FeaturedItems[0].Should().BeEquivalentTo(new
        {
            Group = PlatformActivityGroups.Videos,
            Title = "A video",
            Url = "https://video.example/1",
            Reason = "My first video",
        });
        result.FeaturedItems[1].Reason.Should().BeNull();
        result.BlogPosts.Should().HaveCount(2, "a pinned item stays in its own section too");
    }

    [Fact]
    public void A_pinned_item_in_a_switched_off_section_is_not_featured()
    {
        var result = ProfileDisplay.Apply(With(new ProfileDisplayChoices([PlatformActivityGroups.Videos], [],
            [new ProfilePin("v1", null), new ProfilePin("s1", null)])));

        result.FeaturedItems.Select(i => i.ActivityId).Should().Equal("s1");
    }

    [Fact]
    public void A_pinned_item_without_a_copy_no_longer_in_the_activity_is_skipped()
    {
        var result = ProfileDisplay.Apply(With(new ProfileDisplayChoices([], [], [new ProfilePin("gone", "Old"), new ProfilePin("f1", null)])));

        result.FeaturedItems.Select(i => i.ActivityId).Should().Equal("f1");
    }

    private static ProfileFeaturedItem Copy(string group, string id, string title) =>
        new(group, id, title, $"https://example.dev/{id}", "Kept with the pin", Now.AddYears(-1), "example.dev", null);

    [Fact]
    public void A_pinned_item_no_longer_in_the_activity_is_featured_from_its_copy()
    {
        var result = ProfileDisplay.Apply(With(new ProfileDisplayChoices([], [],
        [
            new ProfilePin("old", "Still proud", Copy(PlatformActivityGroups.Blog, "old", "An old post")),
            new ProfilePin("f1", null),
        ])));

        result.FeaturedItems.Select(i => (i.ActivityId, i.Title, i.Reason)).Should().Equal(
            ("old", "An old post", "Still proud"),
            ("f1", "A thread", null));
        result.FeaturedItems[0].Should().BeEquivalentTo(new
        {
            Group = PlatformActivityGroups.Blog,
            Url = "https://example.dev/old",
            Description = "Kept with the pin",
            Date = (DateTimeOffset?)Now.AddYears(-1),
            Source = "example.dev",
        });
        result.BlogPosts.Should().HaveCount(2, "the copy is only featured, not added back to its section");
    }

    [Fact]
    public void A_pinned_item_still_in_the_activity_is_featured_as_loaded_rather_than_from_its_copy()
    {
        var result = ProfileDisplay.Apply(With(new ProfileDisplayChoices([], [],
            [new ProfilePin("b1", null, Copy(PlatformActivityGroups.Blog, "b1", "An older title"))])));

        result.FeaturedItems.Should().ContainSingle().Which.Title.Should().Be("First post");
    }

    [Fact]
    public void A_pinned_item_shown_from_its_copy_is_not_featured_when_its_section_is_switched_off()
    {
        var result = ProfileDisplay.Apply(With(new ProfileDisplayChoices([PlatformActivityGroups.Blog], [],
            [new ProfilePin("old", null, Copy(PlatformActivityGroups.Blog, "old", "An old post"))])));

        result.FeaturedItems.Should().BeEmpty();
    }

    [Fact]
    public void A_hidden_pinned_item_is_not_featured_from_its_copy()
    {
        var withCopies = With(new ProfileDisplayChoices([], ["b1", "old"],
        [
            new ProfilePin("b1", null, Copy(PlatformActivityGroups.Blog, "b1", "First post")),
            new ProfilePin("old", null, Copy(PlatformActivityGroups.Blog, "old", "An old post")),
        ]));

        ProfileDisplay.Apply(withCopies).FeaturedItems.Should().BeEmpty();
    }

    [Fact]
    public void Saving_pins_takes_a_copy_from_the_activity()
    {
        var choices = new ProfileDisplayChoices([], [], [new ProfilePin("b1", "Proud")]);

        ProfileDisplay.TryTakeSnapshots(choices, ProfileDisplay.ActivityItems(Profile), ProfileDisplayChoices.None, out var saved, out _)
            .Should().BeTrue();

        saved!.Pins.Should().ContainSingle().Which.Should().Be(new ProfilePin("b1", "Proud",
            new ProfileFeaturedItem(PlatformActivityGroups.Blog, "b1", "First post", "https://example.dev/1", "About things", Now, "example.dev", null)));
    }

    [Fact]
    public void Saving_pins_keeps_the_stored_copy_of_an_item_no_longer_in_the_activity()
    {
        var copy = Copy(PlatformActivityGroups.Blog, "old", "An old post");
        var stored = new ProfileDisplayChoices([], [], [new ProfilePin("old", null, copy), new ProfilePin("legacy", null)]);
        var choices = new ProfileDisplayChoices([], [], [new ProfilePin("legacy", null), new ProfilePin("old", "Now with a note")]);

        ProfileDisplay.TryTakeSnapshots(choices, ProfileDisplay.ActivityItems(Profile), stored, out var saved, out _)
            .Should().BeTrue();

        saved!.Pins.Should().Equal(new ProfilePin("legacy", null), new ProfilePin("old", "Now with a note", copy));
    }

    [Fact]
    public void Saving_a_pin_that_is_neither_in_the_activity_nor_stored_is_rejected()
    {
        var choices = new ProfileDisplayChoices([], [], [new ProfilePin("unknown", null)]);

        ProfileDisplay.TryTakeSnapshots(choices, ProfileDisplay.ActivityItems(Profile), ProfileDisplayChoices.None, out _, out var error)
            .Should().BeFalse();
        error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void A_copy_is_cut_to_fit_what_a_pin_stores()
    {
        var profile = Profile with
        {
            BlogPosts = [new ProfileBlogPost(new string('t', 600), "https://example.dev/long", Now, null, ActivityId: "long")],
        };

        ProfileDisplay.TryTakeSnapshots(new ProfileDisplayChoices([], [], [new ProfilePin("long", null)]), ProfileDisplay.ActivityItems(profile), ProfileDisplayChoices.None, out var saved, out _);

        saved!.Pins[0].Snapshot!.Title.Should().HaveLength(MemberPinnedItemEntity.MaxTitleLength).And.EndWith("…");
    }

    [Fact]
    public void Valid_choices_are_trimmed_and_section_keys_made_canonical()
    {
        var ok = ProfileDisplay.TryNormalize(
            Request([" Blog ", "blog", "VIDEOS"], [" b1 ", "b1", "f1"], new UpdateProfilePinRequest(" v1 ", "  Proud of it  ")),
            out var choices,
            out var error);

        ok.Should().BeTrue(error);
        choices!.HiddenSections.Should().Equal(PlatformActivityGroups.Blog, PlatformActivityGroups.Videos);
        choices.HiddenActivityIds.Should().Equal("b1", "f1");
        choices.Pins.Should().Equal(new ProfilePin("v1", "Proud of it"));
    }

    [Fact]
    public void Missing_lists_mean_nothing_chosen()
    {
        ProfileDisplay.TryNormalize(new UpdateProfileDisplayRequest(null, null, null), out var choices, out _).Should().BeTrue();

        choices.Should().BeEquivalentTo(ProfileDisplayChoices.None);
    }

    [Fact]
    public void An_unknown_section_is_rejected()
    {
        ProfileDisplay.TryNormalize(Request(["talks"]), out _, out var error).Should().BeFalse();
        error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Up_to_three_items_can_be_pinned()
    {
        var three = Enumerable.Range(1, ProfileDisplay.MaxPinnedItems).Select(i => new UpdateProfilePinRequest($"a{i}", null)).ToArray();
        var four = Enumerable.Range(1, ProfileDisplay.MaxPinnedItems + 1).Select(i => new UpdateProfilePinRequest($"a{i}", null)).ToArray();

        ProfileDisplay.TryNormalize(Request(pins: three), out _, out _).Should().BeTrue();
        ProfileDisplay.TryNormalize(Request(pins: four), out _, out var error).Should().BeFalse();
        error.Should().Contain("3");
    }

    [Fact]
    public void A_reason_over_the_limit_is_rejected()
    {
        var atLimit = new string('a', ProfileDisplay.MaxPinReasonLength);
        var overLimit = new string('a', ProfileDisplay.MaxPinReasonLength + 1);

        ProfileDisplay.TryNormalize(Request(pins: [new UpdateProfilePinRequest("a1", atLimit)]), out _, out _).Should().BeTrue();
        ProfileDisplay.TryNormalize(Request(pins: [new UpdateProfilePinRequest("a1", overLimit)]), out _, out _).Should().BeFalse();
    }

    [Fact]
    public void An_item_id_over_the_limit_is_rejected()
    {
        var longId = new string('a', ProfileDisplay.MaxActivityIdLength + 1);

        ProfileDisplay.TryNormalize(Request(hiddenItemIds: [longId]), out _, out _).Should().BeFalse();
        ProfileDisplay.TryNormalize(Request(pins: [new UpdateProfilePinRequest(longId, null)]), out _, out _).Should().BeFalse();
    }

    [Fact]
    public void An_empty_item_id_is_rejected()
    {
        ProfileDisplay.TryNormalize(Request(hiddenItemIds: [" "]), out _, out _).Should().BeFalse();
        ProfileDisplay.TryNormalize(Request(pins: [new UpdateProfilePinRequest(null, "Why")]), out _, out _).Should().BeFalse();
    }

    [Fact]
    public void More_hidden_items_than_the_limit_are_rejected()
    {
        var ids = Enumerable.Range(0, ProfileDisplay.MaxHiddenItems + 1).Select(i => $"id{i}").ToList();

        ProfileDisplay.TryNormalize(Request(hiddenItemIds: ids), out _, out _).Should().BeFalse();
    }

    [Fact]
    public void The_same_item_cannot_be_pinned_twice()
    {
        ProfileDisplay.TryNormalize(Request(pins: [new UpdateProfilePinRequest("a1", null), new UpdateProfilePinRequest("a1", "Again")]), out _, out _)
            .Should().BeFalse();
    }

    [Fact]
    public void A_hidden_item_cannot_be_pinned()
    {
        ProfileDisplay.TryNormalize(Request(hiddenItemIds: ["a1"], pins: [new UpdateProfilePinRequest("a1", null)]), out _, out _)
            .Should().BeFalse();
    }

    [Fact]
    public void Activity_items_list_every_section_in_order_and_skip_items_without_an_id()
    {
        var profile = Profile with
        {
            BlogPosts = [.. Profile.BlogPosts, new ProfileBlogPost("Curated", "https://example.dev/c", Now, null)],
        };

        ProfileDisplay.ActivityItems(profile).Select(i => (i.Group, i.ActivityId)).Should().Equal(
            (PlatformActivityGroups.Blog, "b1"),
            (PlatformActivityGroups.Blog, "b2"),
            (PlatformActivityGroups.Forum, "f1"),
            (PlatformActivityGroups.Packages, "p1"),
            (PlatformActivityGroups.Meetups, "m1"),
            (PlatformActivityGroups.Videos, "v1"),
            (PlatformActivityGroups.Social, "s1"));
    }

    [Fact]
    public void Showing_hidden_keeps_the_switched_off_sections_and_hidden_items()
    {
        var result = ProfileDisplay.Apply(With(new ProfileDisplayChoices([PlatformActivityGroups.Videos], ["b1", "m1"], [])), showHidden: true);

        result.BlogPosts.Select(p => p.ActivityId).Should().Equal("b1", "b2");
        result.Videos.Should().HaveCount(1);
        result.Meetups.Should().HaveCount(1);
    }

    [Fact]
    public void Showing_hidden_still_features_the_pins_including_one_in_a_switched_off_section()
    {
        var choices = new ProfileDisplayChoices([PlatformActivityGroups.Videos], [], [new ProfilePin("v1", "Proud"), new ProfilePin("b2", null)]);

        var result = ProfileDisplay.Apply(With(choices), showHidden: true);

        result.FeaturedItems.Select(i => (i.ActivityId, i.Reason)).Should().Equal(("v1", "Proud"), ("b2", null));
    }

    [Theory]
    [InlineData(PlatformActivityGroups.Videos, null, true)]
    [InlineData("VIDEOS", "v9", true)]
    [InlineData(PlatformActivityGroups.Blog, "b1", true)]
    [InlineData(PlatformActivityGroups.Blog, "B1", false)]
    [InlineData(PlatformActivityGroups.Blog, "b2", false)]
    [InlineData(PlatformActivityGroups.Blog, null, false)]
    public void An_item_or_section_is_hidden_by_the_member_when_its_section_is_off_or_its_id_is_hidden(string group, string? activityId, bool expected)
    {
        var choices = new ProfileDisplayChoices([PlatformActivityGroups.Videos], ["b1"], []);

        ProfileDisplay.IsHiddenByMember(choices, group, activityId).Should().Be(expected);
    }

    [Fact]
    public void Only_hidden_sections_or_items_count_as_hiding_anything()
    {
        ProfileDisplay.HidesAnything(ProfileDisplayChoices.None).Should().BeFalse();
        ProfileDisplay.HidesAnything(new ProfileDisplayChoices([], [], [new ProfilePin("b1", null)])).Should().BeFalse();
        ProfileDisplay.HidesAnything(new ProfileDisplayChoices([PlatformActivityGroups.Blog], [], [])).Should().BeTrue();
        ProfileDisplay.HidesAnything(new ProfileDisplayChoices([], ["b1"], [])).Should().BeTrue();
    }
}

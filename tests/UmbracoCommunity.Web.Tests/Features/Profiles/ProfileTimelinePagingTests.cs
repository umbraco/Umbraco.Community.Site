using FluentAssertions;
using UmbracoCommunity.Web.Features.Profiles;
using UmbracoCommunity.Web.Features.Profiles.Models;
using Xunit;

namespace UmbracoCommunity.Web.Tests.Features.Profiles;

public class ProfileTimelinePagingTests
{
    private static IReadOnlyList<ProfileTimelineEntry> Entries(int count, string group = PlatformActivityGroups.Blog) =>
        Enumerable.Range(0, count)
            .Select(i => new ProfileTimelineEntry(group, "id" + i, "Entry " + i, null, null, DateTimeOffset.UtcNow.AddDays(-i), null, null))
            .ToList();

    [Fact]
    public void Entries_are_cut_into_pages_in_order()
    {
        var entries = Entries(ProfileTimeline.PageSize * 2 + 5);

        var first = ProfileTimeline.Paginate(entries, null, true, null);
        var third = ProfileTimeline.Paginate(entries, null, true, "3");

        first.PageCount.Should().Be(3);
        first.Total.Should().Be(entries.Count);
        first.Entries.Should().HaveCount(ProfileTimeline.PageSize).And.StartWith(entries[0]);
        third.Page.Should().Be(3);
        third.Entries.Should().Equal(entries.Skip(ProfileTimeline.PageSize * 2));
    }

    [Theory]
    [InlineData(null, 1)]
    [InlineData("", 1)]
    [InlineData("abc", 1)]
    [InlineData("0", 1)]
    [InlineData("-4", 1)]
    [InlineData("99", 3)]
    public void A_missing_or_bad_page_gives_the_nearest_real_page(string? requested, int expected) =>
        ProfileTimeline.Paginate(Entries(ProfileTimeline.PageSize * 2 + 1), null, true, requested).Page.Should().Be(expected);

    [Fact]
    public void Paging_counts_only_the_entries_that_match_the_filter()
    {
        var entries = Entries(40).Concat(Entries(5, PlatformActivityGroups.Videos)).ToList();

        var videos = ProfileTimeline.Paginate(entries, PlatformActivityGroups.Videos, true, null);

        videos.Total.Should().Be(5);
        videos.PageCount.Should().Be(1);
        videos.Entries.Should().OnlyContain(e => e.Group == PlatformActivityGroups.Videos);
    }

    [Fact]
    public void An_empty_timeline_is_one_empty_page()
    {
        var page = ProfileTimeline.Paginate([], null, true, "2");

        (page.Page, page.PageCount, page.Total).Should().Be((1, 1, 0));
        page.Entries.Should().BeEmpty();
    }
}
